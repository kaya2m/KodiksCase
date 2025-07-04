-- Database Script

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- TABLES

-- Orders Table
CREATE TABLE IF NOT EXISTS Orders (
    Id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    UserId VARCHAR(255) NOT NULL,
    ProductId VARCHAR(255) NOT NULL,
    Quantity INTEGER NOT NULL CHECK (Quantity > 0),
    PaymentMethod VARCHAR(50) NOT NULL CHECK (PaymentMethod IN ('CreditCard', 'BankTransfer')),
    Status VARCHAR(50) NOT NULL DEFAULT 'Pending' CHECK (Status IN ('Pending', 'Processing', 'Processed', 'Completed', 'Cancelled')),
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Order Processing Logs Table
CREATE TABLE IF NOT EXISTS OrderProcessingLogs (
    Id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    OrderId UUID NOT NULL,
    ProcessedAt TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    Status VARCHAR(50) NOT NULL,
    Message TEXT,
    FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE
);

-- INDEXES

-- Orders table indexes
CREATE INDEX IF NOT EXISTS idx_orders_userid ON Orders(UserId);
CREATE INDEX IF NOT EXISTS idx_orders_status ON Orders(Status);
CREATE INDEX IF NOT EXISTS idx_orders_created_at ON Orders(CreatedAt DESC);
CREATE INDEX IF NOT EXISTS idx_orders_payment_method ON Orders(PaymentMethod);
CREATE INDEX IF NOT EXISTS idx_orders_compound_user_status ON Orders(UserId, Status);

-- OrderProcessingLogs table indexes
CREATE INDEX IF NOT EXISTS idx_logs_orderid ON OrderProcessingLogs(OrderId);
CREATE INDEX IF NOT EXISTS idx_logs_processed_at ON OrderProcessingLogs(ProcessedAt DESC);
CREATE INDEX IF NOT EXISTS idx_logs_status ON OrderProcessingLogs(Status);

-- TRIGGERS

-- Function to update UpdatedAt timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.UpdatedAt = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Trigger for Orders table
DROP TRIGGER IF EXISTS update_orders_updated_at ON Orders;
CREATE TRIGGER update_orders_updated_at
    BEFORE UPDATE ON Orders
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- SAMPLE DATA 

-- Insert sample orders
INSERT INTO Orders (Id, UserId, ProductId, Quantity, PaymentMethod, Status, CreatedAt, UpdatedAt)
VALUES 
    (uuid_generate_v4(), 'user123', 'product001', 2, 'CreditCard', 'Completed', CURRENT_TIMESTAMP - INTERVAL '2 days', CURRENT_TIMESTAMP - INTERVAL '2 days'),
    (uuid_generate_v4(), 'user123', 'product002', 1, 'BankTransfer', 'Processed', CURRENT_TIMESTAMP - INTERVAL '1 day', CURRENT_TIMESTAMP - INTERVAL '1 day'),
    (uuid_generate_v4(), 'user456', 'product001', 3, 'CreditCard', 'Pending', CURRENT_TIMESTAMP - INTERVAL '2 hours', CURRENT_TIMESTAMP - INTERVAL '2 hours'),
    (uuid_generate_v4(), 'user789', 'product003', 1, 'BankTransfer', 'Processing', CURRENT_TIMESTAMP - INTERVAL '30 minutes', CURRENT_TIMESTAMP - INTERVAL '30 minutes')
ON CONFLICT (Id) DO NOTHING;

-- Insert sample processing logs
INSERT INTO OrderProcessingLogs (OrderId, Status, Message, ProcessedAt)
SELECT 
    o.Id as OrderId,
    'Processed' as Status,
    'Order processed successfully' as Message,
    o.UpdatedAt as ProcessedAt
FROM Orders o 
WHERE o.Status IN ('Processed', 'Completed')
ON CONFLICT DO NOTHING;

-- VIEWS

-- Order summary view
CREATE OR REPLACE VIEW OrderSummary AS
SELECT 
    o.Id,
    o.UserId,
    o.ProductId,
    o.Quantity,
    o.PaymentMethod,
    o.Status,
    o.CreatedAt,
    o.UpdatedAt,
    COUNT(opl.Id) as LogCount,
    MAX(opl.ProcessedAt) as LastProcessedAt
FROM Orders o
LEFT JOIN OrderProcessingLogs opl ON o.Id = opl.OrderId
GROUP BY o.Id, o.UserId, o.ProductId, o.Quantity, o.PaymentMethod, o.Status, o.CreatedAt, o.UpdatedAt;

-- User order statistics view
CREATE OR REPLACE VIEW UserOrderStats AS
SELECT 
    UserId,
    COUNT(*) as TotalOrders,
    COUNT(CASE WHEN Status = 'Completed' THEN 1 END) as CompletedOrders,
    COUNT(CASE WHEN Status = 'Pending' THEN 1 END) as PendingOrders,
    COUNT(CASE WHEN Status = 'Processing' THEN 1 END) as ProcessingOrders,
    COUNT(CASE WHEN Status = 'Cancelled' THEN 1 END) as CancelledOrders,
    SUM(Quantity) as TotalQuantity,
    MIN(CreatedAt) as FirstOrderDate,
    MAX(CreatedAt) as LastOrderDate
FROM Orders
GROUP BY UserId;

-- STORED PROCEDURES

-- Get user orders with pagination
CREATE OR REPLACE FUNCTION GetUserOrdersPaginated(
    p_user_id VARCHAR(255),
    p_page INTEGER DEFAULT 1,
    p_page_size INTEGER DEFAULT 10
)
RETURNS TABLE (
    Id UUID,
    UserId VARCHAR(255),
    ProductId VARCHAR(255),
    Quantity INTEGER,
    PaymentMethod VARCHAR(50),
    Status VARCHAR(50),
    CreatedAt TIMESTAMP WITH TIME ZONE,
    UpdatedAt TIMESTAMP WITH TIME ZONE,
    TotalCount BIGINT
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        o.Id,
        o.UserId,
        o.ProductId,
        o.Quantity,
        o.PaymentMethod,
        o.Status,
        o.CreatedAt,
        o.UpdatedAt,
        COUNT(*) OVER() as TotalCount
    FROM Orders o
    WHERE o.UserId = p_user_id
    ORDER BY o.CreatedAt DESC
    LIMIT p_page_size
    OFFSET (p_page - 1) * p_page_size;
END;
$$ LANGUAGE plpgsql;

-- Get order with processing logs
CREATE OR REPLACE FUNCTION GetOrderWithLogs(p_order_id UUID)
RETURNS TABLE (
    OrderId UUID,
    UserId VARCHAR(255),
    ProductId VARCHAR(255),
    Quantity INTEGER,
    PaymentMethod VARCHAR(50),
    Status VARCHAR(50),
    CreatedAt TIMESTAMP WITH TIME ZONE,
    UpdatedAt TIMESTAMP WITH TIME ZONE,
    LogId UUID,
    LogStatus VARCHAR(50),
    LogMessage TEXT,
    LogProcessedAt TIMESTAMP WITH TIME ZONE
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        o.Id as OrderId,
        o.UserId,
        o.ProductId,
        o.Quantity,
        o.PaymentMethod,
        o.Status,
        o.CreatedAt,
        o.UpdatedAt,
        opl.Id as LogId,
        opl.Status as LogStatus,
        opl.Message as LogMessage,
        opl.ProcessedAt as LogProcessedAt
    FROM Orders o
    LEFT JOIN OrderProcessingLogs opl ON o.Id = opl.OrderId
    WHERE o.Id = p_order_id
    ORDER BY opl.ProcessedAt DESC;
END;
$$ LANGUAGE plpgsql;

-- PERFORMANCE OPTIMIZATION
ANALYZE Orders;
ANALYZE OrderProcessingLogs;


-- VERIFICATION QUERIES
SELECT 
    schemaname,
    tablename,
    tableowner
FROM pg_tables 
WHERE schemaname = 'public' 
AND tablename IN ('orders', 'orderprocessinglogs');

SELECT 
    indexname,
    tablename,
    indexdef
FROM pg_indexes 
WHERE schemaname = 'public' 
AND tablename IN ('orders', 'orderprocessinglogs');

SELECT 
    conname as constraint_name,
    contype as constraint_type,
    pg_get_constraintdef(c.oid) as constraint_definition
FROM pg_constraint c
JOIN pg_class t ON c.conrelid = t.oid
JOIN pg_namespace n ON t.relnamespace = n.oid
WHERE n.nspname = 'public' 
AND t.relname IN ('orders', 'orderprocessinglogs');

SELECT 
    'Orders' as table_name, 
    COUNT(*) as record_count 
FROM Orders
UNION ALL
SELECT 
    'OrderProcessingLogs' as table_name, 
    COUNT(*) as record_count 
FROM OrderProcessingLogs;



COMMIT; 
