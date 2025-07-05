-- Database Script 

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- TABLES

-- Orders Table (snake_case naming)
CREATE TABLE IF NOT EXISTS orders (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id VARCHAR(255) NOT NULL,
    product_id VARCHAR(255) NOT NULL,
    quantity INTEGER NOT NULL CHECK (quantity > 0),
    payment_method VARCHAR(50) NOT NULL CHECK (payment_method IN ('CreditCard', 'BankTransfer')),
    status VARCHAR(50) NOT NULL DEFAULT 'Pending' CHECK (status IN ('Pending', 'Processing', 'Processed', 'Completed', 'Cancelled')),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Order Processing Logs Table (snake_case naming)
CREATE TABLE IF NOT EXISTS order_processing_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    order_id UUID NOT NULL,
    processed_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    status VARCHAR(50) NOT NULL,
    message TEXT,
    FOREIGN KEY (order_id) REFERENCES orders(id) ON DELETE CASCADE
);

-- INDEXES

-- Orders table indexes
CREATE INDEX IF NOT EXISTS idx_orders_user_id ON orders(user_id);
CREATE INDEX IF NOT EXISTS idx_orders_status ON orders(status);
CREATE INDEX IF NOT EXISTS idx_orders_created_at ON orders(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_orders_payment_method ON orders(payment_method);
CREATE INDEX IF NOT EXISTS idx_orders_compound_user_status ON orders(user_id, status);

-- OrderProcessingLogs table indexes
CREATE INDEX IF NOT EXISTS idx_logs_order_id ON order_processing_logs(order_id);
CREATE INDEX IF NOT EXISTS idx_logs_processed_at ON order_processing_logs(processed_at DESC);
CREATE INDEX IF NOT EXISTS idx_logs_status ON order_processing_logs(status);

-- TRIGGERS

-- Function to update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Trigger for orders table
DROP TRIGGER IF EXISTS update_orders_updated_at ON orders;
CREATE TRIGGER update_orders_updated_at
    BEFORE UPDATE ON orders
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- SAMPLE DATA 

-- Insert sample orders
INSERT INTO orders (id, user_id, product_id, quantity, payment_method, status, created_at, updated_at)
VALUES 
    (uuid_generate_v4(), 'user123', 'product001', 2, 'CreditCard', 'Completed', CURRENT_TIMESTAMP - INTERVAL '2 days', CURRENT_TIMESTAMP - INTERVAL '2 days'),
    (uuid_generate_v4(), 'user123', 'product002', 1, 'BankTransfer', 'Processed', CURRENT_TIMESTAMP - INTERVAL '1 day', CURRENT_TIMESTAMP - INTERVAL '1 day'),
    (uuid_generate_v4(), 'user456', 'product001', 3, 'CreditCard', 'Pending', CURRENT_TIMESTAMP - INTERVAL '2 hours', CURRENT_TIMESTAMP - INTERVAL '2 hours'),
    (uuid_generate_v4(), 'user789', 'product003', 1, 'BankTransfer', 'Processing', CURRENT_TIMESTAMP - INTERVAL '30 minutes', CURRENT_TIMESTAMP - INTERVAL '30 minutes')
ON CONFLICT (id) DO NOTHING;

-- Insert sample processing logs
INSERT INTO order_processing_logs (order_id, status, message, processed_at)
SELECT 
    o.id as order_id,
    'Processed' as status,
    'Order processed successfully' as message,
    o.updated_at as processed_at
FROM orders o 
WHERE o.status IN ('Processed', 'Completed')
ON CONFLICT DO NOTHING;

-- VIEWS

-- Order summary view
CREATE OR REPLACE VIEW order_summary AS
SELECT 
    o.id,
    o.user_id,
    o.product_id,
    o.quantity,
    o.payment_method,
    o.status,
    o.created_at,
    o.updated_at,
    COUNT(opl.id) as log_count,
    MAX(opl.processed_at) as last_processed_at
FROM orders o
LEFT JOIN order_processing_logs opl ON o.id = opl.order_id
GROUP BY o.id, o.user_id, o.product_id, o.quantity, o.payment_method, o.status, o.created_at, o.updated_at;

-- User order statistics view
CREATE OR REPLACE VIEW user_order_stats AS
SELECT 
    user_id,
    COUNT(*) as total_orders,
    COUNT(CASE WHEN status = 'Completed' THEN 1 END) as completed_orders,
    COUNT(CASE WHEN status = 'Pending' THEN 1 END) as pending_orders,
    COUNT(CASE WHEN status = 'Processing' THEN 1 END) as processing_orders,
    COUNT(CASE WHEN status = 'Cancelled' THEN 1 END) as cancelled_orders,
    SUM(quantity) as total_quantity,
    MIN(created_at) as first_order_date,
    MAX(created_at) as last_order_date
FROM orders
GROUP BY user_id;

-- STORED PROCEDURES

-- Get user orders with pagination
CREATE OR REPLACE FUNCTION get_user_orders_paginated(
    p_user_id VARCHAR(255),
    p_page INTEGER DEFAULT 1,
    p_page_size INTEGER DEFAULT 10
)
RETURNS TABLE (
    id UUID,
    user_id VARCHAR(255),
    product_id VARCHAR(255),
    quantity INTEGER,
    payment_method VARCHAR(50),
    status VARCHAR(50),
    created_at TIMESTAMP WITH TIME ZONE,
    updated_at TIMESTAMP WITH TIME ZONE,
    total_count BIGINT
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        o.id,
        o.user_id,
        o.product_id,
        o.quantity,
        o.payment_method,
        o.status,
        o.created_at,
        o.updated_at,
        COUNT(*) OVER() as total_count
    FROM orders o
    WHERE o.user_id = p_user_id
    ORDER BY o.created_at DESC
    LIMIT p_page_size
    OFFSET (p_page - 1) * p_page_size;
END;
$$ LANGUAGE plpgsql;

-- Get order with processing logs
CREATE OR REPLACE FUNCTION get_order_with_logs(p_order_id UUID)
RETURNS TABLE (
    order_id UUID,
    user_id VARCHAR(255),
    product_id VARCHAR(255),
    quantity INTEGER,
    payment_method VARCHAR(50),
    status VARCHAR(50),
    created_at TIMESTAMP WITH TIME ZONE,
    updated_at TIMESTAMP WITH TIME ZONE,
    log_id UUID,
    log_status VARCHAR(50),
    log_message TEXT,
    log_processed_at TIMESTAMP WITH TIME ZONE
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        o.id as order_id,
        o.user_id,
        o.product_id,
        o.quantity,
        o.payment_method,
        o.status,
        o.created_at,
        o.updated_at,
        opl.id as log_id,
        opl.status as log_status,
        opl.message as log_message,
        opl.processed_at as log_processed_at
    FROM orders o
    LEFT JOIN order_processing_logs opl ON o.id = opl.order_id
    WHERE o.id = p_order_id
    ORDER BY opl.processed_at DESC;
END;
$$ LANGUAGE plpgsql;

-- Additional utility functions for the application

-- Get orders by status
CREATE OR REPLACE FUNCTION get_orders_by_status(p_status VARCHAR(50))
RETURNS TABLE (
    id UUID,
    user_id VARCHAR(255),
    product_id VARCHAR(255),
    quantity INTEGER,
    payment_method VARCHAR(50),
    status VARCHAR(50),
    created_at TIMESTAMP WITH TIME ZONE,
    updated_at TIMESTAMP WITH TIME ZONE
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        o.id,
        o.user_id,
        o.product_id,
        o.quantity,
        o.payment_method,
        o.status,
        o.created_at,
        o.updated_at
    FROM orders o
    WHERE o.status = p_status
    ORDER BY o.created_at DESC;
END;
$$ LANGUAGE plpgsql;

-- Get recent orders (last 24 hours)
CREATE OR REPLACE FUNCTION get_recent_orders()
RETURNS TABLE (
    id UUID,
    user_id VARCHAR(255),
    product_id VARCHAR(255),
    quantity INTEGER,
    payment_method VARCHAR(50),
    status VARCHAR(50),
    created_at TIMESTAMP WITH TIME ZONE,
    updated_at TIMESTAMP WITH TIME ZONE
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        o.id,
        o.user_id,
        o.product_id,
        o.quantity,
        o.payment_method,
        o.status,
        o.created_at,
        o.updated_at
    FROM orders o
    WHERE o.created_at >= CURRENT_TIMESTAMP - INTERVAL '24 hours'
    ORDER BY o.created_at DESC;
END;
$$ LANGUAGE plpgsql;

-- PERFORMANCE OPTIMIZATION
ANALYZE orders;
ANALYZE order_processing_logs;

-- VERIFICATION QUERIES
SELECT 
    schemaname,
    tablename,
    tableowner
FROM pg_tables 
WHERE schemaname = 'public' 
AND tablename IN ('orders', 'order_processing_logs');

SELECT 
    indexname,
    tablename,
    indexdef
FROM pg_indexes 
WHERE schemaname = 'public' 
AND tablename IN ('orders', 'order_processing_logs');

SELECT 
    conname as constraint_name,
    contype as constraint_type,
    pg_get_constraintdef(c.oid) as constraint_definition
FROM pg_constraint c
JOIN pg_class t ON c.conrelid = t.oid
JOIN pg_namespace n ON t.relnamespace = n.oid
WHERE n.nspname = 'public' 
AND t.relname IN ('orders', 'order_processing_logs');

SELECT 
    'orders' as table_name, 
    COUNT(*) as record_count 
FROM orders
UNION ALL
SELECT 
    'order_processing_logs' as table_name, 
    COUNT(*) as record_count 
FROM order_processing_logs;

-- Create some additional indexes for better performance
CREATE INDEX IF NOT EXISTS idx_orders_created_at_status ON orders(created_at DESC, status);
CREATE INDEX IF NOT EXISTS idx_orders_user_created ON orders(user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_logs_order_processed ON order_processing_logs(order_id, processed_at DESC);

-- Performance monitoring view
CREATE OR REPLACE VIEW order_performance_stats AS
SELECT 
    DATE(created_at) as order_date,
    COUNT(*) as total_orders,
    COUNT(CASE WHEN status = 'Completed' THEN 1 END) as completed_orders,
    COUNT(CASE WHEN status = 'Pending' THEN 1 END) as pending_orders,
    COUNT(CASE WHEN status = 'Processing' THEN 1 END) as processing_orders,
    COUNT(CASE WHEN status = 'Cancelled' THEN 1 END) as cancelled_orders,
    AVG(quantity) as avg_quantity,
    COUNT(CASE WHEN payment_method = 'CreditCard' THEN 1 END) as credit_card_orders,
    COUNT(CASE WHEN payment_method = 'BankTransfer' THEN 1 END) as bank_transfer_orders
FROM orders
GROUP BY DATE(created_at)
ORDER BY order_date DESC;

COMMIT;