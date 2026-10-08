# Diagramas técnicos

Los diagramas están en [Mermaid](https://mermaid.js.org/): GitHub y Rider los dibujan directamente.

## Modelo entidad-relación

Tablas del negocio (snake_case) y su relación con las tablas de Identity. Todo se crea con la
migración `InitialCreate` de EF Core; no hay scripts SQL.

```mermaid
erDiagram
    AspNetUsers ||--o| customers : "acceso al portal (user_id)"
    AspNetUsers ||--o{ sales : "registra (created_by_user_id)"
    AspNetUsers }o--o{ AspNetRoles : "AspNetUserRoles"
    customers ||--o{ sales : "compra"
    sales ||--|{ sale_details : "contiene"
    products ||--o{ sale_details : "se vende en"

    products {
        uuid id PK "gen_random_uuid()"
        varchar(30) sku UK "mayúsculas"
        varchar(120) name
        varchar(1000) description "nullable"
        varchar(80) category
        varchar(30) unit "default 'Unidad'"
        numeric(14_2) price "CHECK > 0, sin IVA"
        int stock "CHECK >= 0"
        boolean is_active
        timestamptz created_at "now()"
        timestamptz updated_at "nullable"
    }

    customers {
        uuid id PK "gen_random_uuid()"
        varchar(20) document UK
        varchar(120) full_name
        int age "CHECK 18..120"
        varchar(120) email UK "minúsculas"
        varchar(20) phone
        varchar(150) address "nullable"
        boolean is_active
        text user_id FK "nullable, UK"
        timestamptz created_at "now()"
        timestamptz updated_at "nullable"
    }

    sales {
        uuid id PK "gen_random_uuid()"
        varchar(30) sale_number UK "V-yyyyMMdd-0001"
        uuid customer_id FK "RESTRICT"
        timestamptz sale_date "now()"
        varchar(20) status "Completed | Cancelled"
        numeric(14_2) subtotal
        numeric(14_2) tax "IVA 19%"
        numeric(14_2) total "CHECK = subtotal + tax"
        varchar(500) notes "nullable"
        text created_by_user_id FK "SET NULL"
        timestamptz cancelled_at "nullable"
    }

    sale_details {
        uuid id PK "gen_random_uuid()"
        uuid sale_id FK "CASCADE"
        uuid product_id FK "RESTRICT"
        int quantity "CHECK > 0"
        numeric(14_2) unit_price "precio al momento de la venta"
        numeric(14_2) line_total
    }

    AspNetUsers {
        text Id PK
        varchar Email UK
        text FullName
    }

    AspNetRoles {
        text Id PK
        varchar Name "Administrador | Cliente"
    }
```

Reglas que el modelo protege:

- Un producto o cliente con ventas **no se puede borrar** (`RESTRICT`): se desactiva con `is_active`.
- `sale_details.unit_price` congela el precio: si el producto cambia de precio, las ventas viejas no cambian.
- Los detalles se borran con su venta (`CASCADE`), pero las ventas nunca se borran: se anulan (`status = Cancelled`) y el stock vuelve al inventario.

## Diagrama de clases (arquitectura limpia)

Las dependencias apuntan hacia adentro: `Admin → Application → Domain`, e `Infrastructure` implementa
las interfaces de `Application`.

```mermaid
classDiagram
    direction LR

    namespace Domain {
        class Product {
            +Guid Id
            +string Sku
            +string Name
            +string Category
            +string Unit
            +decimal Price
            +int Stock
            +bool IsActive
        }
        class Customer {
            +Guid Id
            +string Document
            +string FullName
            +int Age
            +string Email
            +string Phone
            +string? UserId
            +bool IsActive
        }
        class Sale {
            +Guid Id
            +string SaleNumber
            +DateTimeOffset SaleDate
            +SaleStatus Status
            +decimal Subtotal
            +decimal Tax
            +decimal Total
        }
        class SaleDetail {
            +int Quantity
            +decimal UnitPrice
            +decimal LineTotal
        }
        class SaleStatus {
            <<enumeration>>
            Completed
            Cancelled
        }
        class InventoryCalculator {
            <<static>>
            +TaxRate = 0.19
            +CalculateLineTotal(qty, price) decimal
            +CalculateTotals(lines) SaleTotals
            +DiscountStock(stock, qty) int
        }
        class SaleNumberGenerator {
            <<static>>
            +Generate(date, seq) string
        }
    }

    namespace Application {
        class IProductRepository {
            <<interface>>
        }
        class ICustomerRepository {
            <<interface>>
        }
        class ISaleRepository {
            <<interface>>
        }
        class IUnitOfWork {
            <<interface>>
            +SaveChangesAsync()
            +BeginTransactionAsync()
        }
        class ProductService {
            +ListAsync(ProductQuery)
            +CreateAsync(ProductRequest) Result
            +UpdateAsync(id, ProductRequest) Result
            +DeleteAsync(id) Result
        }
        class CustomerService {
            +ListAsync(CustomerQuery)
            +CreateAsync(CustomerRequest) Result
            +UpdateAsync(id, CustomerRequest) Result
            +DeleteAsync(id) Result
        }
        class SaleService {
            +ListAsync(SaleQuery)
            +CreateAsync(SaleRequest, userId) Result
            +CancelAsync(id) Result
        }
        class InputParser {
            <<static>>
            +ParseAge(text) Result~int~
        }
    }

    namespace Infrastructure {
        class ApplicationDbContext
        class ProductRepository
        class CustomerRepository
        class SaleRepository
        class UnitOfWork
        class IdentitySeeder
    }

    namespace Admin {
        class ProductsController
        class CustomersController
        class SalesController
        class AccountController
    }

    Sale "1" *-- "1..*" SaleDetail
    Sale "*" --> "1" Customer
    SaleDetail "*" --> "1" Product
    Sale --> SaleStatus

    ProductService ..> IProductRepository
    ProductService ..> IUnitOfWork
    CustomerService ..> ICustomerRepository
    SaleService ..> ISaleRepository
    SaleService ..> IProductRepository
    SaleService ..> ICustomerRepository
    SaleService ..> InventoryCalculator
    SaleService ..> SaleNumberGenerator

    ProductRepository ..|> IProductRepository
    CustomerRepository ..|> ICustomerRepository
    SaleRepository ..|> ISaleRepository
    UnitOfWork ..|> IUnitOfWork
    ProductRepository --> ApplicationDbContext
    CustomerRepository --> ApplicationDbContext
    SaleRepository --> ApplicationDbContext

    ProductsController ..> ProductService
    CustomersController ..> CustomerService
    CustomersController ..> InputParser
    SalesController ..> SaleService
```

## Flujo de una venta

```mermaid
sequenceDiagram
    actor A as Administrador
    participant C as SalesController
    participant S as SaleService
    participant R as Repositorios
    participant DB as PostgreSQL

    A->>C: POST /Sales/Create (cliente + líneas)
    C->>S: CreateAsync(SaleRequest, userId)
    S->>R: FindByIdAsync(cliente)
    S->>DB: BEGIN
    S->>R: FindByIdsForUpdateAsync(productos)
    R->>DB: SELECT ... FOR UPDATE
    S->>S: valida stock de TODAS las líneas
    S->>S: descuenta stock, calcula subtotal + IVA
    S->>R: CountByNumberPrefixAsync("V-yyyyMMdd-")
    S->>R: AddAsync(venta)
    S->>DB: SaveChanges + COMMIT
    S-->>C: Result<SaleResponse>
    C-->>A: Redirect a /Sales/Details/{id}
```
