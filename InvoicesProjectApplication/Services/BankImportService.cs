using System.Globalization;
using System.Text;
using System.Xml.Linq;
using InvoicesProjectApplication.DTOs;
using InvoicesProjectApplication.Interfaces;
using InvoicesProjectEntities.Enums;
using Microsoft.Extensions.Logging;

namespace InvoicesProjectApplication.Services;

public class BankImportService : IBankImportService
{
    private readonly IDebtService _debtService;
    private readonly IReceivableService _receivableService;
    private readonly ICardPurchaseService _cardPurchaseService;
    private readonly ILogger<BankImportService> _logger;

    public BankImportService(
        IDebtService debtService,
        IReceivableService receivableService,
        ICardPurchaseService cardPurchaseService,
        ILogger<BankImportService> logger)
    {
        _debtService = debtService;
        _receivableService = receivableService;
        _cardPurchaseService = cardPurchaseService;
        _logger = logger;
    }

    public async Task<BankImportResultDto> ParseOfxAsync(Stream fileStream, string fileName)
    {
        _logger.LogInformation("Parseando arquivo OFX: {FileName}", fileName);

        string content;
        using (var reader = new StreamReader(fileStream, Encoding.GetEncoding("ISO-8859-1"), detectEncodingFromByteOrderMarks: true))
        {
            content = await reader.ReadToEndAsync();
        }

        // OFX pode ser SGML (mais antigo) ou XML. Converter SGML para XML se necessário
        var xmlContent = ConvertOfxToXml(content);
        var doc = XDocument.Parse(xmlContent);

        var transactions = new List<BankTransactionDto>();
        string? bankName = null;
        string? accountId = null;

        // Extrair info da conta
        var bankAcctFrom = doc.Descendants("BANKACCTFROM").FirstOrDefault()
            ?? doc.Descendants("CCACCTFROM").FirstOrDefault();
        if (bankAcctFrom is not null)
        {
            accountId = bankAcctFrom.Element("ACCTID")?.Value;
        }

        var fi = doc.Descendants("FI").FirstOrDefault();
        if (fi is not null)
        {
            bankName = fi.Element("ORG")?.Value;
        }

        // Extrair transações
        var stmtTrns = doc.Descendants("STMTTRN");
        foreach (var trn in stmtTrns)
        {
            var trnType = trn.Element("TRNTYPE")?.Value ?? "OTHER";
            var datePosted = trn.Element("DTPOSTED")?.Value;
            var amount = trn.Element("TRNAMT")?.Value;
            var name = trn.Element("NAME")?.Value;
            var memo = trn.Element("MEMO")?.Value;

            if (amount is null) continue;

            var parsedAmount = ParseOfxAmount(amount);
            var parsedDate = ParseOfxDate(datePosted);
            var description = name ?? memo ?? "Transação sem descrição";

            var transactionType = parsedAmount >= 0 ? "credit" : "debit";
            var category = GuessCategory(description);

            transactions.Add(new BankTransactionDto(
                description,
                Math.Abs(parsedAmount),
                parsedDate.ToString("yyyy-MM-dd"),
                transactionType,
                category,
                memo
            ));
        }

        _logger.LogInformation("Extraídas {Count} transações do OFX {FileName}", transactions.Count, fileName);

        return new BankImportResultDto(
            fileName,
            bankName,
            accountId,
            transactions.Count,
            transactions
        );
    }

    public async Task<BankImportResultDto> ParseCsvAsync(Stream fileStream, string fileName)
    {
        _logger.LogInformation("Parseando arquivo CSV: {FileName}", fileName);

        var transactions = new List<BankTransactionDto>();

        using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var headerLine = await reader.ReadLineAsync();
        if (headerLine is null)
            return new BankImportResultDto(fileName, null, null, 0, transactions);

        var headers = headerLine.Split([',', ';'])
            .Select(h => h.Trim().Trim('"').ToLowerInvariant())
            .ToList();

        // Detectar colunas (suporta vários formatos de CSV de bancos brasileiros)
        var dateIdx = headers.FindIndex(h =>
            h.Contains("data") || h == "date" || h.Contains("dtposted"));
        var descIdx = headers.FindIndex(h =>
            h.Contains("descri") || h.Contains("memo") || h.Contains("histórico") ||
            h.Contains("historico") || h == "name" || h.Contains("lançamento") || h.Contains("lancamento"));
        var amountIdx = headers.FindIndex(h =>
            h.Contains("valor") || h == "amount" || h.Contains("vl.") || h.Contains("quantia"));

        if (dateIdx == -1 || descIdx == -1 || amountIdx == -1)
            throw new ArgumentException(
                $"CSV não possui colunas reconhecíveis. Colunas encontradas: {string.Join(", ", headers)}. " +
                "Esperado: coluna de data, descrição e valor.");

        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = SplitCsvLine(line);
            if (cols.Count <= Math.Max(dateIdx, Math.Max(descIdx, amountIdx))) continue;

            var dateStr = cols[dateIdx].Trim('"');
            var desc = cols[descIdx].Trim('"');
            var amountStr = cols[amountIdx].Trim('"');

            if (string.IsNullOrWhiteSpace(desc) || string.IsNullOrWhiteSpace(amountStr)) continue;

            var amount = ParseBrazilianAmount(amountStr);
            var date = ParseFlexibleDate(dateStr);
            var transactionType = amount >= 0 ? "credit" : "debit";
            var category = GuessCategory(desc);

            transactions.Add(new BankTransactionDto(
                desc,
                Math.Abs(amount),
                date.ToString("yyyy-MM-dd"),
                transactionType,
                category,
                null
            ));
        }

        _logger.LogInformation("Extraídas {Count} transações do CSV {FileName}", transactions.Count, fileName);

        return new BankImportResultDto(fileName, null, null, transactions.Count, transactions);
    }

    public async Task<ImportResultDto> ConfirmImportAsync(Guid userId, ConfirmBankImportDto dto)
    {
        var details = new List<string>();
        int debtsCreated = 0, cardPurchasesCreated = 0;

        foreach (var txn in dto.Transactions)
        {
            try
            {
                if (txn.TransactionType == "credit")
                {
                    var receivableDto = new CreateReceivableDto(
                        txn.Description,
                        txn.Amount,
                        DateOnly.TryParse(txn.Date, out var expectedDate) ? expectedDate : DateOnly.FromDateTime(DateTime.UtcNow),
                        txn.Memo);
                    await _receivableService.CreateAsync(userId, receivableDto);
                    details.Add($"✅ Recebível '{txn.Description}' R${txn.Amount:F2}");
                }
                else if (!string.IsNullOrEmpty(dto.CreditCardId))
                {
                    var purchaseDto = new CreateCardPurchaseDto(
                        Guid.Parse(dto.CreditCardId),
                        txn.Description,
                        txn.Amount,
                        DateOnly.TryParse(txn.Date, out var purchaseDateOnly)
                            ? purchaseDateOnly
                            : DateOnly.FromDateTime(DateTime.UtcNow),
                        1,
                        txn.Memo,
                        txn.Category);
                    await _cardPurchaseService.CreateAsync(purchaseDto);
                    cardPurchasesCreated++;
                    details.Add($"✅ Compra '{txn.Description}' R${txn.Amount:F2}");
                }
                else
                {
                    var debtDto = new CreateDebtDto(
                        txn.Description,
                        txn.Amount,
                        DateOnly.TryParse(txn.Date, out var dueDate) ? dueDate : DateOnly.FromDateTime(DateTime.UtcNow),
                        txn.Memo,
                        txn.Category);
                    await _debtService.CreateAsync(userId, debtDto);
                    debtsCreated++;
                    details.Add($"✅ Débito '{txn.Description}' R${txn.Amount:F2}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao importar transação: {Description}", txn.Description);
                details.Add($"❌ Erro em '{txn.Description}': {ex.Message}");
            }
        }

        return new ImportResultDto(dto.Transactions.Count, debtsCreated, cardPurchasesCreated, details);
    }

    // ─── Helpers ───

    private static string ConvertOfxToXml(string ofxContent)
    {
        // Encontrar o início do XML/SGML
        var ofxStart = ofxContent.IndexOf("<OFX>", StringComparison.OrdinalIgnoreCase);
        if (ofxStart == -1)
            throw new ArgumentException("Arquivo OFX inválido: tag <OFX> não encontrada.");

        var sgml = ofxContent[ofxStart..];

        // Tags auto-fechantes do SGML OFX não têm barra, adicionar tag de fechamento
        var sb = new StringBuilder();
        var lines = sgml.Split('\n');
        var tagStack = new Stack<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            if (line.StartsWith("</"))
            {
                sb.AppendLine(line);
                if (tagStack.Count > 0) tagStack.Pop();
            }
            else if (line.StartsWith('<'))
            {
                var tagEnd = line.IndexOf('>');
                if (tagEnd == -1) continue;

                var tag = line[1..tagEnd];
                var afterTag = line[(tagEnd + 1)..].Trim();

                if (string.IsNullOrEmpty(afterTag))
                {
                    // Container tag
                    sb.AppendLine(line);
                    tagStack.Push(tag);
                }
                else if (!afterTag.StartsWith('<'))
                {
                    // Data tag: <TAG>value → <TAG>value</TAG>
                    sb.AppendLine($"<{tag}>{afterTag}</{tag}>");
                }
                else
                {
                    sb.AppendLine(line);
                }
            }
        }

        var result = sb.ToString();
        // Garantir declaração XML
        if (!result.TrimStart().StartsWith("<?xml"))
            result = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + result;

        return result;
    }

    private static decimal ParseOfxAmount(string amount)
    {
        amount = amount.Trim().Replace("+", "");
        return decimal.TryParse(amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) ? val : 0;
    }

    private static DateOnly ParseOfxDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return DateOnly.FromDateTime(DateTime.UtcNow);

        // OFX formato: YYYYMMDDHHMMSS[.XXX:GMT] ou YYYYMMDD
        var datePart = dateStr.Length >= 8 ? dateStr[..8] : dateStr;
        return DateOnly.TryParseExact(datePart, "yyyyMMdd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private static decimal ParseBrazilianAmount(string amount)
    {
        amount = amount.Trim().Replace("R$", "").Replace(" ", "");

        // Detectar formato brasileiro (1.234,56) vs americano (1,234.56)
        var lastComma = amount.LastIndexOf(',');
        var lastDot = amount.LastIndexOf('.');

        if (lastComma > lastDot && lastComma == amount.Length - 3)
        {
            // Formato brasileiro: 1.234,56
            amount = amount.Replace(".", "").Replace(",", ".");
        }

        return decimal.TryParse(amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) ? val : 0;
    }

    private static DateOnly ParseFlexibleDate(string dateStr)
    {
        dateStr = dateStr.Trim();
        string[] formats = ["dd/MM/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "MM/dd/yyyy", "dd/MM/yy", "yyyyMMdd"];
        foreach (var fmt in formats)
        {
            if (DateOnly.TryParseExact(dateStr, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d;
        }
        return DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var inQuotes = false;
        var field = new StringBuilder();

        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if ((c == ',' || c == ';') && !inQuotes)
            {
                result.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(c);
            }
        }
        result.Add(field.ToString());
        return result;
    }

    private static string GuessCategory(string description)
    {
        var lower = description.ToLowerInvariant();

        if (lower.Contains("uber") || lower.Contains("99") || lower.Contains("cabify") ||
            lower.Contains("combustivel") || lower.Contains("posto") || lower.Contains("estacionamento"))
            return "Transporte";

        if (lower.Contains("ifood") || lower.Contains("restaurante") || lower.Contains("padaria") ||
            lower.Contains("lanche") || lower.Contains("pizza") || lower.Contains("almoço") || lower.Contains("almoco"))
            return "Alimentação";

        if (lower.Contains("mercado") || lower.Contains("super") || lower.Contains("atacad") ||
            lower.Contains("hortifruti") || lower.Contains("feira"))
            return "Mercado";

        if (lower.Contains("netflix") || lower.Contains("spotify") || lower.Contains("disney") ||
            lower.Contains("amazon prime") || lower.Contains("hbo") || lower.Contains("youtube") ||
            lower.Contains("assinatura"))
            return "Assinaturas";

        if (lower.Contains("farmacia") || lower.Contains("drogaria") || lower.Contains("hospital") ||
            lower.Contains("medic") || lower.Contains("dentista") || lower.Contains("consulta"))
            return "Saúde";

        if (lower.Contains("aluguel") || lower.Contains("condominio") || lower.Contains("iptu") ||
            lower.Contains("luz") || lower.Contains("energia") || lower.Contains("agua") || lower.Contains("gás") || lower.Contains("gas"))
            return "Moradia";

        if (lower.Contains("escola") || lower.Contains("faculdade") || lower.Contains("curso") ||
            lower.Contains("livro") || lower.Contains("educacao") || lower.Contains("mensalidade"))
            return "Educação";

        if (lower.Contains("cinema") || lower.Contains("teatro") || lower.Contains("show") ||
            lower.Contains("ingresso") || lower.Contains("lazer") || lower.Contains("game") || lower.Contains("jogo"))
            return "Lazer";

        if (lower.Contains("presente") || lower.Contains("gift"))
            return "Presentes";

        if (lower.Contains("pix") || lower.Contains("transf"))
            return "Transferências";

        return "Outros";
    }
}
