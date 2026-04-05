using System;
using System.Collections.Generic;
using InvoicesProjectEntities.Entities;

namespace InvoicesProjectEntities.Entities
{
    /// <summary>
    /// Representa um agrupador temporal de gastos (ex: viagem, evento, reforma)
    /// </summary>
    public class TagEvento : BaseEntity
    {
        public string Nome { get; set; } = string.Empty;
        public string? Descricao { get; set; }
        public DateTime? DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;
        // Lançamentos associados (opcional, para navegação)
        public virtual ICollection<Debt> Debts { get; set; } = new List<Debt>();
        public virtual ICollection<Receivable> Receivables { get; set; } = new List<Receivable>();
        public virtual ICollection<CardPurchase> CardPurchases { get; set; } = new List<CardPurchase>();
    }
}
