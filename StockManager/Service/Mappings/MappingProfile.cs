using AutoMapper;
using StockManager.Models.DTOs;
using StockManager.Models.Entities;

namespace StockManager.Service.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Products
        CreateMap<Product, ProductResponse>()
            .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Category.Name))
            .ForMember(d => d.SupplierName, o => o.MapFrom(s => s.Supplier != null ? s.Supplier.Name : null))
            .ForMember(d => d.ImageUrl, o => o.MapFrom(s => s.ImagePath))
            .ForMember(d => d.TotalStock, o => o.MapFrom(s => s.StockLevels.Sum(l => l.Quantity)));

        CreateMap<ProductRequest, Product>()
            .ForMember(d => d.Name, o => o.MapFrom(s => s.Name.Trim()))
            .ForMember(d => d.Sku, o => o.MapFrom(s => s.Sku.Trim().ToUpperInvariant()))
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.ImagePath, o => o.Ignore())
            .ForMember(d => d.Category, o => o.Ignore())
            .ForMember(d => d.Supplier, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore())
            .ForMember(d => d.StockLevels, o => o.Ignore())
            .ForMember(d => d.StockMovements, o => o.Ignore());

        // Categories
        CreateMap<Category, CategoryResponse>()
            .ForMember(d => d.ProductCount, o => o.MapFrom(s => s.Products.Count));

        CreateMap<CategoryRequest, Category>()
            .ForMember(d => d.Name, o => o.MapFrom(s => s.Name.Trim()))
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.Products, o => o.Ignore());

        // Suppliers
        CreateMap<Supplier, SupplierResponse>();

        CreateMap<SupplierRequest, Supplier>()
            .ForMember(d => d.Name, o => o.MapFrom(s => s.Name.Trim()))
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.Products, o => o.Ignore())
            .ForMember(d => d.StockMovements, o => o.Ignore());

        // WareHouses
        CreateMap<Warehouse, WarehouseResponse>();

        CreateMap<WarehouseRequest, Warehouse>()
            .ForMember(d => d.Name, o => o.MapFrom(s => s.Name.Trim()))
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.StockLevels, o => o.Ignore())
            .ForMember(d => d.StockMovements, o => o.Ignore());

        CreateMap<StockLevel, StockLevelResponse>()
            .ForMember(d => d.ProductName, o => o.MapFrom(s => s.Product.Name))
            .ForMember(d => d.ProductSku, o => o.MapFrom(s => s.Product.Sku))
            .ForMember(d => d.WarehouseName, o => o.MapFrom(s => s.Warehouse.Name));

        // Stock movement
        CreateMap<StockMovement, StockMovementResponse>()
            .ForMember(d => d.ProductName, o => o.MapFrom(s => s.Product.Name))
            .ForMember(d => d.ProductSku, o => o.MapFrom(s => s.Product.Sku))
            .ForMember(d => d.WarehouseName, o => o.MapFrom(s => s.Warehouse.Name))
            .ForMember(d => d.SupplierName, o => o.MapFrom(s => s.Supplier != null ? s.Supplier.Name : null))
            .ForMember(d => d.CreatedBy, o => o.MapFrom(s => s.CreatedByUserName));

        CreateMap<StockReceiptRequest, RegisterStockMovementRequest>()
            .ForMember(d => d.Type, o => o.MapFrom(_ => MovementType.Incoming))
            .ForMember(d => d.Reason, o => o.MapFrom(s => s.Note));

        CreateMap<StockWriteOffRequest, RegisterStockMovementRequest>()
            .ForMember(d => d.Type, o => o.MapFrom(_ => MovementType.Outgoing))
            .ForMember(d => d.SupplierId, o => o.Ignore())
            .ForMember(d => d.UnitCost, o => o.Ignore());

        // Audit
        CreateMap<AuditLog, AuditLogResponse>();
    }
}
