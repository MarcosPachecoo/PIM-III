CREATE DATABASE IF NOT EXISTS napoli_express_pizzaria;

USE napoli_express_pizzaria;

SET FOREIGN_KEY_CHECKS = 0;

DROP TABLE IF EXISTS chamados;
DROP TABLE IF EXISTS bebidas;
DROP TABLE IF EXISTS pizzas;
DROP TABLE IF EXISTS itens_pedido;
DROP TABLE IF EXISTS pedidos;
DROP TABLE IF EXISTS funcionarios;
DROP TABLE IF EXISTS clientes;

SET FOREIGN_KEY_CHECKS = 1;

-- =========================================
-- TABELA CLIENTES
-- =========================================

CREATE TABLE clientes (
    id_cliente INT PRIMARY KEY AUTO_INCREMENT,
    nome VARCHAR(100),
    email VARCHAR(100),
    telefone VARCHAR(20),
    senha VARCHAR(100),
    data_cadastro DATE
);

INSERT INTO clientes (nome, email, telefone, senha, data_cadastro)
VALUES
('Ana Souza', 'ana@gmail.com', '11987654321', '123456', '2023-01-10'),
('Bruno Lima', 'bruno@gmail.com', '11991234567', 'abcdef', '2023-06-25'),
('Carla Mendes', 'carla@gmail.com', '11999887766', 'senha123', '2024-02-14'),
('Diego Alves', 'diego@gmail.com', '11995554433', 'qwerty', '2024-09-30'),
('Juliana Rocha', 'juliana@gmail.com', '11993332211', 'juliana01', '2025-12-05');

-- =========================================
-- TABELA FUNCIONARIOS
-- =========================================

CREATE TABLE funcionarios (
    id_funcionario INT PRIMARY KEY AUTO_INCREMENT,
    nome VARCHAR(100),
    email VARCHAR(100),
    senha VARCHAR(100),
    cargo VARCHAR(50)
);

INSERT INTO funcionarios (nome, email, senha, cargo)
VALUES
('Joao Silva', 'joao@napoliexpress.com', '123', 'Gerente'),
('Maria Souza', 'maria@napoliexpress.com', '456', 'Caixa'),
('Carlos Lima', 'carlos@napoliexpress.com', '789', 'Entregador'),
('Ana Paula', 'ana@napoliexpress.com', '111', 'Atendente'),
('Pedro Santos', 'pedro@napoliexpress.com', '222', 'Pizzaiolo'),
('Lucas Oliveira', 'lucas@napoliexpress.com', '333', 'Motoboy'),
('Fernanda Alves', 'fernanda@napoliexpress.com', '444', 'Caixa');

-- =========================================
-- TABELA PEDIDOS
-- =========================================

CREATE TABLE pedidos (
    id_pedido INT PRIMARY KEY AUTO_INCREMENT,
    endereco_entrega VARCHAR(200),
    forma_pagamento VARCHAR(50),
    total_pedido DECIMAL(10,2),
    status_pedido VARCHAR(50),
    data_hora DATETIME,
    id_cliente INT,
    id_funcionario INT,
    FOREIGN KEY (id_cliente) REFERENCES clientes(id_cliente),
    FOREIGN KEY (id_funcionario) REFERENCES funcionarios(id_funcionario)
);

INSERT INTO pedidos (endereco_entrega, forma_pagamento, total_pedido, status_pedido, data_hora, id_cliente, id_funcionario)
VALUES
('Rua das Flores, 120', 'Cartao de Credito', 64.80, 'Entregue', '2025-05-10 19:30:00', 1, 1),
('Av. Paulista, 450', 'PIX', 67.80, 'Em preparo', '2025-05-11 20:15:00', 2, 2),
('Rua Central, 89', 'Dinheiro', 39.90, 'Entregue', '2025-05-12 18:40:00', 3, 4),
('Rua Aurora, 300', 'Cartao de Debito', 82.80, 'Saiu para entrega', '2025-05-13 21:00:00', 4, 3),
('Av. Brasil, 1500', 'PIX', 44.80, 'Em preparo', '2025-05-14 19:10:00', 5, 2);

-- =========================================
-- TABELA ITENS_PEDIDO
-- =========================================

CREATE TABLE itens_pedido (
    id_item INT PRIMARY KEY AUTO_INCREMENT,
    tipo_item VARCHAR(50),
    nome_item VARCHAR(100),
    quantidade INT,
    valor_unitario DECIMAL(10,2),
    subtotal DECIMAL(10,2),
    id_pedido INT,
    FOREIGN KEY (id_pedido) REFERENCES pedidos(id_pedido)
);

INSERT INTO itens_pedido (tipo_item, nome_item, quantidade, valor_unitario, subtotal, id_pedido)
VALUES
('Pizza', 'Calabresa', 1, 49.90, 49.90, 1),
('Bebida', 'Coca-Cola 2L', 1, 14.90, 14.90, 1),
('Pizza', 'Portuguesa', 1, 54.90, 54.90, 2),
('Bebida', 'Guarana Antartica 2L', 1, 12.90, 12.90, 2),
('Pizza', 'Mussarela', 1, 39.90, 39.90, 3);

-- =========================================
-- TABELA PIZZAS
-- =========================================

CREATE TABLE pizzas (
    id_pizza INT PRIMARY KEY AUTO_INCREMENT,
    tamanho VARCHAR(30),
    quantidade_fatias INT,
    sabores VARCHAR(200),
    borda VARCHAR(100),
    valor_da_pizza DECIMAL(10,2),
    valor_da_borda DECIMAL(10,2),
    id_item INT,
    FOREIGN KEY (id_item) REFERENCES itens_pedido(id_item)
);

INSERT INTO pizzas (tamanho, quantidade_fatias, sabores, borda, valor_da_pizza, valor_da_borda, id_item)
VALUES
('Grande', 8, 'Calabresa', 'Catupiry', 49.90, 8.90, 1),
('Grande', 8, 'Portuguesa', 'Cheddar', 54.90, 10.90, 3),
('Media', 6, 'Mussarela', 'Sem borda', 39.90, 0.00, 5),
('Familia', 12, 'Frango com Catupiry', 'Chocolate', 69.90, 12.90, 4),
('Pequena', 4, 'Quatro Queijos', 'Catupiry', 34.90, 7.90, 5);

-- =========================================
-- TABELA BEBIDAS
-- =========================================

CREATE TABLE bebidas (
    id_bebida INT PRIMARY KEY AUTO_INCREMENT,
    nome_bebida VARCHAR(100),
    quantidade INT,
    valor DECIMAL(10,2),
    id_item INT,
    FOREIGN KEY (id_item) REFERENCES itens_pedido(id_item)
);

INSERT INTO bebidas (nome_bebida, quantidade, valor, id_item)
VALUES
('Coca-Cola 2L', 1, 14.90, 2),
('Guarana Antartica 2L', 2, 12.90, 4),
('Fanta Laranja 2L', 1, 11.90, 3),
('Agua Mineral 500ml', 2, 4.50, 4),
('Suco de Laranja 1L', 1, 9.90, 5);

-- =========================================
-- TABELA CHAMADOS
-- =========================================

CREATE TABLE chamados (
    id_chamado INT PRIMARY KEY AUTO_INCREMENT,
    nome_cliente VARCHAR(100),
    email VARCHAR(100),
    telefone VARCHAR(20),
    unidade VARCHAR(100),
    assunto VARCHAR(100),
    mensagem TEXT,
    status VARCHAR(50),
    data_hora DATETIME
);

INSERT INTO chamados (nome_cliente, email, telefone, unidade, assunto, mensagem, status, data_hora)
VALUES
('Ana Souza', 'ana@gmail.com', '11987654321', 'Napoli Express Osasco', 'Atraso no Pedido', 'O pedido demorou mais de 1 hora para chegar.', 'Resolvido', '2025-05-15 20:30:00'),
('Bruno Lima', 'bruno@gmail.com', '11991234567', 'Napoli Express Barueri', 'Pedido Errado', 'Recebi sabor diferente do solicitado.', 'Em analise', '2025-05-16 19:10:00'),
('Juliana Rocha', 'juliana@gmail.com', '11993332211', 'Napoli Express Carapicuiba', 'Elogio', 'Excelente atendimento e entrega rapida.', 'Finalizado', '2025-05-17 21:00:00');
