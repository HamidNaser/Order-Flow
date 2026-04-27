// MongoDB Migration Script
const dbName = "orders";

print(`[ MongoDB Migration ] Connection: ${db.getMongo()._uri}`);
print(`[ MongoDB Migration ] Starting migrations for database: ${dbName}`);

// Switch to the target database
db = db.getSiblingDB(dbName);

// Create collections
print("[ MongoDB Migration ] Creating 'orders' collection...");
db.createCollection("orders");

print("[ MongoDB Migration | orders ] Creating index on StoreId...");
db.orders.createIndex(
    { StoreId: 1 },
    { name: "StoreId_ASC" }
);

print("[ MongoDB Migration | orders ] Creating composite index on StoreId, CustomerId, and OrderDateUtc...");
db.orders.createIndex(
    { StoreId: 1, CustomerId: 1, OrderDateUtc: -1 },
    { name: "StoreId_ASC_CustomerId_ASC_OrderDateUtc_DESC" }
);

print("[ MongoDB Migration | orders ] Creating composite index on StoreId, Merchant.OrderId, Merchant.Name, and type...");
db.orders.createIndex(
    { StoreId: 1, "Merchant.OrderId": 1, "Merchant.Name": 1, "_t": 1 },
    { name: "StoreId_ASC_Merchant.OrderId_ASC_Merchant.Name_ASC__t_ASC" }
);
