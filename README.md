# mechanics-work-orders

Microsserviço responsável por:

- `Customers`
- `Vehicles`
- `WorkOrders`
- `WorkOrderHistories`

Este serviço **não** implementa regras de `Billing` nem de `Execution`.

## Ambiente

- SDK: .NET 8
- Banco: SQL Server
- E-mail local: MailPit
- Mensageria: SQS (LocalStack em ambiente local)

## Comunicação entre serviços

### REST síncrono (CrossServiceClient)

- Consulta ao `mechanics-identity` para validação de usuário externo (ex.: mecânico por `AssignedToUserId`).
- Endpoints de consulta por ID aceitam role `SERVICE`.

### Eventos assíncronos (SQS)

Publishers:

- `customer-created`
- `work-order-created`

Consumers (fila preparada, implementação futura):

- `status-changed`
- `payment-approved`

## Execução local

Suba dependências:

```bash
docker compose up mssql mailpit localstack -d
```

Rode a API:

```bash
dotnet run --project ./src/Mechanics.Api/Mechanics.Api.csproj
```

Swagger:

- [http://localhost:5000/work-orders/swagger](http://localhost:5000/work-orders/swagger)

## Migrações

Criar migração:

```powershell
dotnet ef migrations add Init --project src/Mechanics.Infra.Data --startup-project src/Mechanics.Api
```

Aplicar no banco:

```powershell
dotnet ef database update --project src/Mechanics.Infra.Data --startup-project src/Mechanics.Api
```

## Testes

```bash
dotnet build Mechanics.Example.sln
dotnet test Mechanics.Example.sln
```

Observação: testes de integração dependem de Docker/Testcontainers ativo.
