namespace MiniTime.Core.Models;

/// <summary>Nome da tabela SQLite da entidade.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class TableAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

/// <summary>Chave primária (coluna única).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class KeyAttribute : Attribute { }

/// <summary>Coluna gerada pelo banco (AUTOINCREMENT); não entra no INSERT.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class IdentityAttribute : Attribute { }

/// <summary>Propriedade que não é coluna.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotMappedAttribute : Attribute { }
