# ARCHITECTURE — minitime

```
MiniTime.App (WPF net8.0-windows)
   ├─ MiniTime.Data   (SQLite, repositórios, importador MDB → SQLite)
   │       └─ processo filho: MiniTime.MdbReader.exe (net48, x86, Jet 4.0) → NDJSON
   ├─ MiniTime.Serial (porta serial / Mini Point)
   └─ MiniTime.Core   (modelos, apuração, formatos)
```
O leitor do MDB é um exe x86 separado porque Jet 4.0 só existe em 32 bits e o app roda em 64 bits.
