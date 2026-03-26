namespace InvoicesProjectEntities.Enums;

public static class ExpenseCategory
{
    public const string Alimentacao = "Alimentação";
    public const string Moradia = "Moradia";
    public const string Transporte = "Transporte";
    public const string Saude = "Saúde";
    public const string Educacao = "Educação";
    public const string Lazer = "Lazer";
    public const string Vestuario = "Vestuário";
    public const string Assinaturas = "Assinaturas";
    public const string Mercado = "Mercado";
    public const string Pets = "Pets";
    public const string Presentes = "Presentes";
    public const string Viagem = "Viagem";
    public const string Tecnologia = "Tecnologia";
    public const string Servicos = "Serviços";
    public const string Familia = "Família";
    public const string Investimentos = "Investimentos";
    public const string Impostos = "Impostos";
    public const string Outros = "Outros";

    public static readonly string[] All =
    [
        Alimentacao, Moradia, Transporte, Saude, Educacao, Lazer,
        Vestuario, Assinaturas, Mercado, Pets, Presentes, Viagem,
        Tecnologia, Servicos, Familia, Investimentos, Impostos, Outros
    ];

    public static bool IsValid(string? category) =>
        category is null || All.Contains(category, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return Outros;
        var match = All.FirstOrDefault(c => c.Equals(category, StringComparison.OrdinalIgnoreCase));
        return match ?? Outros;
    }
}
