-- ============================================================
-- Seed inicial: Clinsoft — dados de referência
-- Execute após a primeira migration
-- ============================================================

USE ClinSoft;
GO

-- Empresa padrão (ajuste CNPJ e dados reais antes de usar em produção)
IF NOT EXISTS (SELECT 1 FROM Empresas)
BEGIN
    INSERT INTO Empresas (Id, RazaoSocial, NomeFantasia, Cnpj, InscricaoEstadual, InscricaoMunicipal,
        RegimeTributario, Logradouro, Numero, Bairro, Cidade, Uf, Cep, Telefone, Email, Ativo,
        CriadoEm, AtualizadoEm)
    VALUES (NEWID(), 'Loja de Produtos Naturais LTDA', 'Vida Natural',
        '00000000000000', '', '', 'SN',
        'Rua das Flores', '100', 'Centro', 'São Paulo', 'SP', '01310100',
        '(11) 3000-0000', 'contato@vidanatural.com.br', 1, GETDATE(), NULL);
END
GO

-- Unidades de medida padrão
DECLARE @EmpresaId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Empresas);

IF NOT EXISTS (SELECT 1 FROM UnidadesMedida WHERE EmpresaId = @EmpresaId)
BEGIN
    INSERT INTO UnidadesMedida (Id, EmpresaId, Sigla, Descricao, Ativo, CriadoEm, AtualizadoEm) VALUES
        (NEWID(), @EmpresaId, 'UN',  'Unidade',       1, GETDATE(), NULL),
        (NEWID(), @EmpresaId, 'CX',  'Caixa',         1, GETDATE(), NULL),
        (NEWID(), @EmpresaId, 'KG',  'Quilograma',    1, GETDATE(), NULL),
        (NEWID(), @EmpresaId, 'G',   'Grama',         1, GETDATE(), NULL),
        (NEWID(), @EmpresaId, 'L',   'Litro',         1, GETDATE(), NULL),
        (NEWID(), @EmpresaId, 'ML',  'Mililitro',     1, GETDATE(), NULL),
        (NEWID(), @EmpresaId, 'PCT', 'Pacote',        1, GETDATE(), NULL),
        (NEWID(), @EmpresaId, 'FR',  'Frasco',        1, GETDATE(), NULL),
        (NEWID(), @EmpresaId, 'CP',  'Cápsula',       1, GETDATE(), NULL),
        (NEWID(), @EmpresaId, 'SC',  'Sachê',         1, GETDATE(), NULL);
END
GO

-- Categorias padrão para loja de produtos naturais
DECLARE @EmpresaId2 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Empresas);

IF NOT EXISTS (SELECT 1 FROM Categorias WHERE EmpresaId = @EmpresaId2)
BEGIN
    INSERT INTO Categorias (Id, EmpresaId, Nome, CategoriaPaiId, Ativo, CriadoEm, AtualizadoEm) VALUES
        (NEWID(), @EmpresaId2, 'Suplementos',          NULL, 1, GETDATE(), NULL),
        (NEWID(), @EmpresaId2, 'Vitaminas e Minerais', NULL, 1, GETDATE(), NULL),
        (NEWID(), @EmpresaId2, 'Fitoterápicos',        NULL, 1, GETDATE(), NULL),
        (NEWID(), @EmpresaId2, 'Alimentos Naturais',   NULL, 1, GETDATE(), NULL),
        (NEWID(), @EmpresaId2, 'Cosméticos Naturais',  NULL, 1, GETDATE(), NULL),
        (NEWID(), @EmpresaId2, 'Chás e Infusões',      NULL, 1, GETDATE(), NULL),
        (NEWID(), @EmpresaId2, 'Óleos Essenciais',     NULL, 1, GETDATE(), NULL),
        (NEWID(), @EmpresaId2, 'Grãos e Sementes',     NULL, 1, GETDATE(), NULL),
        (NEWID(), @EmpresaId2, 'Proteínas',            NULL, 1, GETDATE(), NULL),
        (NEWID(), @EmpresaId2, 'Outros',               NULL, 1, GETDATE(), NULL);
END
GO

-- Marcas padrão
DECLARE @EmpresaId3 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Empresas);

IF NOT EXISTS (SELECT 1 FROM Marcas WHERE EmpresaId = @EmpresaId3)
BEGIN
    INSERT INTO Marcas (Id, EmpresaId, Nome, Ativo, CriadoEm, AtualizadoEm) VALUES
        (NEWID(), @EmpresaId3, 'Marca Própria', 1, GETDATE(), NULL),
        (NEWID(), @EmpresaId3, 'Sem Marca',     1, GETDATE(), NULL);
END
GO

-- Local de estoque principal
DECLARE @EmpresaId4 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Empresas);

IF NOT EXISTS (SELECT 1 FROM LocaisEstoque WHERE EmpresaId = @EmpresaId4)
BEGIN
    INSERT INTO LocaisEstoque (Id, EmpresaId, Nome, Descricao, Principal, Ativo, CriadoEm, AtualizadoEm) VALUES
        (NEWID(), @EmpresaId4, 'Loja Principal', 'Estoque principal da loja', 1, 1, GETDATE(), NULL),
        (NEWID(), @EmpresaId4, 'Depósito',       'Depósito/armazém',         0, 1, GETDATE(), NULL);
END
GO

-- Usuário administrador padrão
-- Senha: Admin@123 (hash BCrypt — altere imediatamente em produção)
DECLARE @EmpresaId5 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Empresas);

IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE EmpresaId = @EmpresaId5)
BEGIN
    INSERT INTO Usuarios (Id, EmpresaId, Nome, Email, SenhaHash, Perfil, Ativo, CriadoEm, AtualizadoEm)
    VALUES (NEWID(), @EmpresaId5, 'Administrador', 'admin@clinsoft.com.br',
        '$2a$11$placeholder_troque_antes_de_usar_em_producao_XXXXX',
        'Administrador', 1, GETDATE(), NULL);
END
GO

PRINT 'Seed concluído com sucesso.';
