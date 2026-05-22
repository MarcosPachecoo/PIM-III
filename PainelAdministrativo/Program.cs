using System.Security.Claims;
using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/sair";
        options.AccessDeniedPath = "/login";
        options.Cookie.Name = "NapoliFuncionario";
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("SitePublico", policy =>
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseStaticFiles();
app.UseCors("SitePublico");
app.UseAuthentication();
app.UseAuthorization();

var funcionarios = new Dictionary<string, Funcionario>
{
    ["admin@napolipizzaria.com.br"] = new("Admin Napoli", "admin123", "Gerente"),
    ["atendimento@napolipizzaria.com.br"] = new("Atendimento Napoli", "pizza123", "Atendimento")
};

var chamadosPadrao = new List<Chamado>
{
    new(1001, "Pedido atrasado", "Cliente informou atraso na entrega.", "Aberto", "cliente@exemplo.com"),
    new(1002, "Troca de sabor", "Cliente solicitou troca antes do preparo.", "Em atendimento", "maria@exemplo.com"),
    new(1003, "Problema no pagamento", "Pagamento via cartão não foi confirmado.", "Aberto", "joao@exemplo.com")
};

var conexaoBanco = builder.Configuration.GetConnectionString("NapoliExpress")
    ?? builder.Configuration["MYSQL_CONNECTION_STRING"]
    ?? "Server=localhost;Port=3306;Database=napoli_express_pizzaria;User ID=root;Password=;CharSet=utf8mb4;";

CriarEstruturaBanco(conexaoBanco);

var pedidos = CarregarPedidos(conexaoBanco);
var chamados = CarregarChamados(conexaoBanco);
if (chamados.Count == 0)
{
    foreach (var chamadoPadrao in chamadosPadrao)
    {
        InserirChamado(conexaoBanco, new ChamadoEntrada(
            chamadoPadrao.ClienteNome,
            chamadoPadrao.Cliente,
            chamadoPadrao.ClienteTelefone,
            chamadoPadrao.Unidade,
            chamadoPadrao.Assunto,
            chamadoPadrao.Descricao));
    }

    chamados = CarregarChamados(conexaoBanco);
}

app.MapGet("/", () => Results.Redirect("/painel"));

app.MapPost("/api/pedidos", (PedidoEntrada pedidoEntrada) =>
{
    if (string.IsNullOrWhiteSpace(pedidoEntrada.ClienteNome) ||
        string.IsNullOrWhiteSpace(pedidoEntrada.ClienteEmail) ||
        string.IsNullOrWhiteSpace(pedidoEntrada.Endereco) ||
        string.IsNullOrWhiteSpace(pedidoEntrada.FormaPagamento) ||
        pedidoEntrada.Itens is null ||
        pedidoEntrada.Itens.Count == 0 ||
        pedidoEntrada.Total <= 0)
    {
        return Results.BadRequest(new { mensagem = "Dados do pedido incompletos." });
    }

    var pedido = InserirPedido(conexaoBanco, pedidoEntrada);
    pedidos = CarregarPedidos(conexaoBanco);

    return Results.Created($"/api/pedidos/{pedido.Id}", new
    {
        pedido.Id,
        mensagem = "Pedido recebido no painel administrativo."
    });
}).AllowAnonymous();

app.MapPost("/api/chamados", (ChamadoEntrada chamadoEntrada) =>
{
    if (string.IsNullOrWhiteSpace(chamadoEntrada.ClienteNome) ||
        string.IsNullOrWhiteSpace(chamadoEntrada.ClienteEmail) ||
        string.IsNullOrWhiteSpace(chamadoEntrada.Assunto) ||
        string.IsNullOrWhiteSpace(chamadoEntrada.Descricao))
    {
        return Results.BadRequest(new { mensagem = "Dados do chamado incompletos." });
    }

    var chamado = InserirChamado(conexaoBanco, chamadoEntrada);
    chamados = CarregarChamados(conexaoBanco);

    return Results.Created($"/api/chamados/{chamado.Id}", new
    {
        chamado.Id,
        mensagem = "Chamado recebido no painel administrativo."
    });
}).AllowAnonymous();

app.MapGet("/login", (HttpContext contexto) =>
{
    if (contexto.User.Identity?.IsAuthenticated == true)
    {
        return Results.Redirect("/painel");
    }

    return Results.Content(PaginaLogin(), "text/html; charset=utf-8");
}).AllowAnonymous();

app.MapPost("/login", async (HttpContext contexto) =>
{
    var formulario = await contexto.Request.ReadFormAsync();
    var email = formulario["email"].ToString().Trim().ToLowerInvariant();
    var senha = formulario["senha"].ToString();

    if (!funcionarios.TryGetValue(email, out var funcionario) || funcionario.Senha != senha)
    {
        return Results.Content(PaginaLogin("E-mail ou senha de funcionário inválidos."), "text/html; charset=utf-8");
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.Name, funcionario.Nome),
        new(ClaimTypes.Email, email),
        new(ClaimTypes.Role, funcionario.Cargo)
    };

    var identidade = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await contexto.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidade));

    return Results.Redirect("/painel");
}).AllowAnonymous();

app.MapGet("/sair", async (HttpContext contexto) =>
{
    await contexto.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

var painel = app.MapGroup("").RequireAuthorization();

painel.MapGet("/painel", (HttpContext contexto) =>
{
    pedidos = CarregarPedidos(conexaoBanco);
    chamados = CarregarChamados(conexaoBanco);

    var nomeFuncionario = contexto.User.Identity?.Name ?? "Funcionário";
    var chamadosAbertos = chamados.Count(chamado => chamado.Status != "Resolvido");
    var pedidosHoje = pedidos.Count(pedido => pedido.DataHora.Date == DateTime.Today);
    var faturamentoHoje = pedidos
        .Where(pedido => pedido.DataHora.Date == DateTime.Today)
        .Sum(pedido => pedido.Total);
    var totalGeralPedidos = pedidos.Count;
    var pizzaMaisVendida = CalcularPizzaMaisVendida(pedidos);
    var pedidosPorStatus = ContarPedidosPorStatus(pedidos);

    return Results.Content(
        PaginaPainel(nomeFuncionario, chamadosAbertos, pedidosHoje, faturamentoHoje, totalGeralPedidos, pizzaMaisVendida, pedidosPorStatus),
        "text/html; charset=utf-8");
});

painel.MapGet("/pedidos", () =>
{
    pedidos = CarregarPedidos(conexaoBanco);
    return Results.Content(PaginaPedidos(pedidos), "text/html; charset=utf-8");
});

painel.MapPost("/pedidos/{id:int}/status", async (int id, HttpContext contexto) =>
{
    var formulario = await contexto.Request.ReadFormAsync();
    var novoStatus = formulario["status"].ToString();
    var pedido = pedidos.FirstOrDefault(item => item.Id == id);

    if (pedido is not null && !string.IsNullOrWhiteSpace(novoStatus))
    {
        AtualizarStatusPedido(conexaoBanco, id, novoStatus);
        pedidos = CarregarPedidos(conexaoBanco);
    }

    return Results.Redirect("/pedidos");
});

painel.MapGet("/chamados", () =>
{
    chamados = CarregarChamados(conexaoBanco);
    return Results.Content(PaginaChamados(chamados), "text/html; charset=utf-8");
});

painel.MapPost("/chamados/{id:int}/status", async (int id, HttpContext contexto) =>
{
    var formulario = await contexto.Request.ReadFormAsync();
    var novoStatus = formulario["status"].ToString();
    var chamado = chamados.FirstOrDefault(item => item.Id == id);

    if (chamado is not null && !string.IsNullOrWhiteSpace(novoStatus))
    {
        AtualizarStatusChamado(conexaoBanco, id, novoStatus);
        chamados = CarregarChamados(conexaoBanco);
    }

    return Results.Redirect("/chamados");
});

app.Run();

static string Layout(string titulo, string conteudo, bool mostrarMenu = true)
{
    var menu = mostrarMenu
        ? """
          <header class="topo">
              <strong>Napoli Express</strong>
              <nav>
                  <a href="/painel">Painel</a>
                  <a href="/pedidos">Pedidos</a>
                  <a href="/chamados">Contatos</a>
                  <a href="/sair">Sair</a>
              </nav>
          </header>
          """
        : "";

    return $$"""
    <!DOCTYPE html>
    <html lang="pt-BR">
    <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>{{titulo}} - Painel Napoli</title>
        <link rel="stylesheet" href="/estilo.css">
    </head>
    <body>
        {{menu}}
        <main class="conteudo">
            {{conteudo}}
        </main>
    </body>
    </html>
    """;
}

static string PaginaLogin(string mensagem = "")
{
    var alerta = string.IsNullOrWhiteSpace(mensagem) ? "" : $"""<p class="erro">{mensagem}</p>""";

    return Layout("Login", $$"""
    <section class="cartao login">
        <h1>Painel Administrativo</h1>
        <p class="subtitulo">Acesso exclusivo para funcionários da pizzaria.</p>
        {{alerta}}
        <form method="post" action="/login">
            <label for="email">E-mail do funcionário</label>
            <input id="email" name="email" type="email" placeholder="admin@napolipizzaria.com.br" required>

            <label for="senha">Senha</label>
            <input id="senha" name="senha" type="password" placeholder="Senha do funcionário" required>

            <button type="submit">Entrar</button>
        </form>
        <small>Exemplo para apresentação: admin@napolipizzaria.com.br / admin123</small>
    </section>
    """, mostrarMenu: false);
}

static string PaginaPainel(
    string nomeFuncionario,
    int chamadosAbertos,
    int pedidosHoje,
    decimal faturamentoHoje,
    int totalGeralPedidos,
    string pizzaMaisVendida,
    Dictionary<string, int> pedidosPorStatus)
{
    var linhasStatus = pedidosPorStatus.Count == 0
        ? "<li>Nenhum pedido recebido ainda.</li>"
        : string.Join("", pedidosPorStatus.Select(status =>
            $"""<li><span>{Texto(status.Key)}</span><strong>{status.Value}</strong></li>"""));

    return Layout("Painel", $$"""
    <section class="titulo-pagina">
        <h1>Olá, {{nomeFuncionario}}</h1>
        <p>Resumo administrativo da Napoli Express.</p>
    </section>

    <section class="grade-resumo">
        <article class="cartao resumo">
            <span>Contatos em aberto</span>
            <strong>{{chamadosAbertos}}</strong>
        </article>
        <article class="cartao resumo">
            <span>Pedidos de hoje</span>
            <strong>{{pedidosHoje}}</strong>
        </article>
        <article class="cartao resumo">
            <span>Faturamento de hoje</span>
            <strong>{{faturamentoHoje.ToString("C", new System.Globalization.CultureInfo("pt-BR"))}}</strong>
        </article>
        <article class="cartao resumo">
            <span>Total geral de pedidos</span>
            <strong>{{totalGeralPedidos}}</strong>
        </article>
        <article class="cartao resumo">
            <span>Pizza mais vendida</span>
            <strong class="texto-indicador">{{Texto(pizzaMaisVendida)}}</strong>
        </article>
    </section>

    <section class="cartao indicadores-status">
        <h2>Pedidos por status</h2>
        <ul>
            {{linhasStatus}}
        </ul>
    </section>

    <section class="cartao">
        <h2>Ações rápidas</h2>
        <a class="botao-link" href="/pedidos">Consultar pedidos recebidos</a>
        <a class="botao-link" href="/chamados">Ver reclamações, sugestões e informações</a>
    </section>
    """);
}

static string PaginaPedidos(List<Pedido> pedidos)
{
    var conteudo = pedidos.Count == 0
        ? """<section class="cartao"><p class="vazio">Nenhum pedido recebido ainda.</p></section>"""
        : $$"""
          <section class="lista-pedidos">
              {{string.Join("", pedidos.Select(PedidoHtml))}}
          </section>
          """;

    return Layout("Pedidos", $$"""
    <section class="titulo-pagina">
        <h1>Pedidos dos Clientes</h1>
        <p>Pedidos enviados pelo site público aparecem aqui para os funcionários.</p>
    </section>

    {{conteudo}}
    """);
}

static string PaginaChamados(List<Chamado> chamados)
{
    var totalInformacoes = chamados.Count(chamado => chamado.Assunto.Equals("Informações", StringComparison.OrdinalIgnoreCase));
    var totalSugestoes = chamados.Count(chamado => chamado.Assunto.Equals("Sugestões", StringComparison.OrdinalIgnoreCase));
    var totalReclamacoes = chamados.Count(chamado => chamado.Assunto.Equals("Reclamações", StringComparison.OrdinalIgnoreCase));

    var linhas = chamados.Count == 0
        ? """<section class="cartao"><p class="vazio">Nenhum contato recebido ainda.</p></section>"""
        : $$"""
          <section class="lista-chamados">
              {{string.Join("", chamados.Select(ContatoHtml))}}
          </section>
          """;

    return Layout("Contatos", $$"""
    <section class="titulo-pagina">
        <h1>Reclamações, sugestões e informações</h1>
        <p>Todos os contatos enviados pelo site, com os dados de cada cliente.</p>
    </section>

    <section class="grade-resumo resumo-contatos">
        <article class="cartao resumo">
            <span>Informações</span>
            <strong>{{totalInformacoes}}</strong>
        </article>
        <article class="cartao resumo">
            <span>Sugestões</span>
            <strong>{{totalSugestoes}}</strong>
        </article>
        <article class="cartao resumo">
            <span>Reclamações</span>
            <strong>{{totalReclamacoes}}</strong>
        </article>
    </section>

    {{linhas}}
    """);
}

static string ContatoHtml(Chamado chamado)
{
    var telefone = string.IsNullOrWhiteSpace(chamado.ClienteTelefone) ? "Não informado" : chamado.ClienteTelefone;
    var unidade = string.IsNullOrWhiteSpace(chamado.Unidade) ? "Não informada" : chamado.Unidade;

    return $$"""
    <article class="chamado contato-cliente">
        <div class="contato-dados">
            <h2>#{{chamado.Id}} - {{Texto(chamado.Assunto)}}</h2>
            <div class="dados-cliente">
                <span><strong>Nome:</strong> {{Texto(chamado.ClienteNome)}}</span>
                <span><strong>Email:</strong> {{Texto(chamado.Cliente)}}</span>
                <span><strong>Telefone:</strong> {{Texto(telefone)}}</span>
                <span><strong>Unidade:</strong> {{Texto(unidade)}}</span>
                <span><strong>Recebido em:</strong> {{chamado.DataHora.ToString("dd/MM/yyyy HH:mm")}}</span>
            </div>
            <p>{{Texto(chamado.Descricao)}}</p>
        </div>
        <form method="post" action="/chamados/{{chamado.Id}}/status">
            <label>Status</label>
            <select name="status">
                {{OpcaoStatus("Aberto", chamado.Status)}}
                {{OpcaoStatus("Em atendimento", chamado.Status)}}
                {{OpcaoStatus("Resolvido", chamado.Status)}}
            </select>
            <button type="submit">Salvar</button>
        </form>
    </article>
    """;
}
static string PedidoHtml(Pedido pedido)
{
    var itens = string.Join("", pedido.Itens.Select(item =>
        $"""<li>{Texto(item.Quantidade.ToString())}x {Texto(item.Nome)} - {item.Valor.ToString("C", new System.Globalization.CultureInfo("pt-BR"))}</li>"""));

    return $$"""
    <article class="pedido">
        <div class="pedido-dados">
            <h2>Pedido #{{pedido.Id}}</h2>
            <p><strong>Cliente:</strong> {{Texto(pedido.ClienteNome)}} - {{Texto(pedido.ClienteEmail)}}</p>
            <p><strong>Endereço:</strong> {{Texto(pedido.Endereco)}}</p>
            <p><strong>Pagamento:</strong> {{Texto(pedido.FormaPagamento)}}</p>
            <p><strong>Data:</strong> {{pedido.DataHora.ToString("dd/MM/yyyy HH:mm")}}</p>
            <p><strong>Total:</strong> {{pedido.Total.ToString("C", new System.Globalization.CultureInfo("pt-BR"))}}</p>
            <ul>{{itens}}</ul>
        </div>
        <form method="post" action="/pedidos/{{pedido.Id}}/status">
            <label>Status</label>
            <select name="status">
                {{OpcaoStatus("Recebido", pedido.Status)}}
                {{OpcaoStatus("Em preparo", pedido.Status)}}
                {{OpcaoStatus("Saiu para entrega", pedido.Status)}}
                {{OpcaoStatus("Finalizado", pedido.Status)}}
            </select>
            <button type="submit">Salvar</button>
        </form>
    </article>
    """;
}

static string OpcaoStatus(string status, string statusAtual)
{
    var selecionado = status == statusAtual ? "selected" : "";
    return $"""<option value="{status}" {selecionado}>{status}</option>""";
}

static string Texto(string valor)
{
    return WebUtility.HtmlEncode(valor);
}

static string CalcularPizzaMaisVendida(List<Pedido> pedidos)
{
    var pizzaMaisVendida = pedidos
        .SelectMany(pedido => pedido.Itens)
        .Where(item => item.Nome.StartsWith("Pizza", StringComparison.OrdinalIgnoreCase))
        .GroupBy(item => item.Nome)
        .Select(grupo => new
        {
            Nome = grupo.Key,
            Quantidade = grupo.Sum(item => item.Quantidade)
        })
        .OrderByDescending(item => item.Quantidade)
        .FirstOrDefault();

    return pizzaMaisVendida is null
        ? "Sem pedidos"
        : $"{pizzaMaisVendida.Nome} ({pizzaMaisVendida.Quantidade})";
}

static Dictionary<string, int> ContarPedidosPorStatus(List<Pedido> pedidos)
{
    return pedidos
        .GroupBy(pedido => string.IsNullOrWhiteSpace(pedido.Status) ? "Sem status" : pedido.Status)
        .OrderBy(grupo => grupo.Key)
        .ToDictionary(grupo => grupo.Key, grupo => grupo.Count());
}

static void CriarEstruturaBanco(string conexaoBanco)
{
    using var conexao = new MySqlConnection(conexaoBanco);
    conexao.Open();

    var comandos = new[]
    {
        """
        CREATE TABLE IF NOT EXISTS clientes (
            id_cliente INT PRIMARY KEY AUTO_INCREMENT,
            nome VARCHAR(100),
            email VARCHAR(100),
            telefone VARCHAR(20),
            senha VARCHAR(100),
            data_cadastro DATE
        ) DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        """,
        """
        CREATE TABLE IF NOT EXISTS pedidos (
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
        ) DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        """,
        """
        CREATE TABLE IF NOT EXISTS itens_pedido (
            id_item INT PRIMARY KEY AUTO_INCREMENT,
            tipo_item VARCHAR(50),
            nome_item VARCHAR(100),
            quantidade INT,
            valor_unitario DECIMAL(10,2),
            subtotal DECIMAL(10,2),
            id_pedido INT,
            FOREIGN KEY (id_pedido) REFERENCES pedidos(id_pedido)
        ) DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        """,
        """
        CREATE TABLE IF NOT EXISTS pizzas (
            id_pizza INT PRIMARY KEY AUTO_INCREMENT,
            tamanho VARCHAR(30),
            quantidade_fatias INT,
            sabores VARCHAR(200),
            borda VARCHAR(100),
            valor_da_pizza DECIMAL(10,2),
            valor_da_borda DECIMAL(10,2),
            id_item INT,
            FOREIGN KEY (id_item) REFERENCES itens_pedido(id_item)
        ) DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        """,
        """
        CREATE TABLE IF NOT EXISTS bebidas (
            id_bebida INT PRIMARY KEY AUTO_INCREMENT,
            nome_bebida VARCHAR(100),
            quantidade INT,
            valor DECIMAL(10,2),
            id_item INT,
            FOREIGN KEY (id_item) REFERENCES itens_pedido(id_item)
        ) DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        """,
        """
        CREATE TABLE IF NOT EXISTS funcionarios (
            id_funcionario INT PRIMARY KEY AUTO_INCREMENT,
            nome VARCHAR(100),
            email VARCHAR(100),
            senha VARCHAR(100),
            cargo VARCHAR(50)
        ) DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        """,
        """
        CREATE TABLE IF NOT EXISTS chamados (
            id_chamado INT PRIMARY KEY AUTO_INCREMENT,
            nome_cliente VARCHAR(100),
            email VARCHAR(100),
            telefone VARCHAR(20),
            unidade VARCHAR(100),
            assunto VARCHAR(100),
            mensagem TEXT,
            status VARCHAR(50),
            data_hora DATETIME
        ) DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        """
    };

    foreach (var textoComando in comandos)
    {
        using var comando = new MySqlCommand(textoComando, conexao);
        comando.ExecuteNonQuery();
    }

    using var comandoFuncionario = new MySqlCommand(
        """
        INSERT INTO funcionarios (id_funcionario, nome, email, senha, cargo)
        SELECT 1, 'João Silva', 'joao@napoliexpress.com', '123', 'Gerente'
        WHERE NOT EXISTS (SELECT 1 FROM funcionarios WHERE id_funcionario = 1);
        """,
        conexao);
    comandoFuncionario.ExecuteNonQuery();
}

static List<Pedido> CarregarPedidos(string conexaoBanco)
{
    var pedidos = new List<Pedido>();
    var pedidosPorId = new Dictionary<int, Pedido>();

    using var conexao = new MySqlConnection(conexaoBanco);
    conexao.Open();

    using var comando = new MySqlCommand(
        """
        SELECT
            p.id_pedido,
            c.nome,
            c.email,
            p.endereco_entrega,
            p.forma_pagamento,
            p.total_pedido,
            p.data_hora,
            p.status_pedido,
            i.nome_item,
            i.quantidade,
            i.subtotal
        FROM pedidos p
        LEFT JOIN clientes c ON c.id_cliente = p.id_cliente
        LEFT JOIN itens_pedido i ON i.id_pedido = p.id_pedido
        ORDER BY p.data_hora DESC, p.id_pedido DESC, i.id_item ASC;
        """,
        conexao);

    using var leitor = comando.ExecuteReader();
    while (leitor.Read())
    {
        var idPedido = leitor.GetInt32("id_pedido");
        if (!pedidosPorId.TryGetValue(idPedido, out var pedido))
        {
            pedido = new Pedido(
                idPedido,
                LerTexto(leitor, "nome"),
                LerTexto(leitor, "email"),
                LerTexto(leitor, "endereco_entrega"),
                LerTexto(leitor, "forma_pagamento"),
                new List<ItemPedido>(),
                LerDecimal(leitor, "total_pedido"),
                LerData(leitor, "data_hora"),
                LerTexto(leitor, "status_pedido"));

            pedidosPorId[idPedido] = pedido;
            pedidos.Add(pedido);
        }

        if (!leitor.IsDBNull(leitor.GetOrdinal("nome_item")))
        {
            pedido.Itens.Add(new ItemPedido(
                LerTexto(leitor, "nome_item"),
                LerInteiro(leitor, "quantidade"),
                LerDecimal(leitor, "subtotal")));
        }
    }

    return pedidos;
}

static List<Chamado> CarregarChamados(string conexaoBanco)
{
    var chamados = new List<Chamado>();

    using var conexao = new MySqlConnection(conexaoBanco);
    conexao.Open();

    using var comando = new MySqlCommand(
        """
        SELECT id_chamado, nome_cliente, email, telefone, unidade, assunto, mensagem, status, data_hora
        FROM chamados
        ORDER BY data_hora DESC, id_chamado DESC;
        """,
        conexao);

    using var leitor = comando.ExecuteReader();
    while (leitor.Read())
    {
        chamados.Add(new Chamado(
            leitor.GetInt32("id_chamado"),
            LerTexto(leitor, "assunto"),
            LerTexto(leitor, "mensagem"),
            LerTexto(leitor, "status"),
            LerTexto(leitor, "email"),
            LerTexto(leitor, "nome_cliente"),
            LerTexto(leitor, "telefone"),
            LerTexto(leitor, "unidade"),
            LerData(leitor, "data_hora")));
    }

    return chamados;
}

static Pedido InserirPedido(string conexaoBanco, PedidoEntrada pedidoEntrada)
{
    using var conexao = new MySqlConnection(conexaoBanco);
    conexao.Open();

    using var transacao = conexao.BeginTransaction();

    try
    {
        var idCliente = InserirCliente(conexao, transacao, pedidoEntrada.ClienteNome, pedidoEntrada.ClienteEmail);

        using var comandoPedido = new MySqlCommand(
            """
            INSERT INTO pedidos
            (endereco_entrega, forma_pagamento, total_pedido, status_pedido, data_hora, id_cliente, id_funcionario)
            VALUES
            (@endereco, @pagamento, @total, @status, NOW(), @idCliente, @idFuncionario);
            SELECT LAST_INSERT_ID();
            """,
            conexao,
            transacao);

        comandoPedido.Parameters.AddWithValue("@endereco", pedidoEntrada.Endereco);
        comandoPedido.Parameters.AddWithValue("@pagamento", pedidoEntrada.FormaPagamento);
        comandoPedido.Parameters.AddWithValue("@total", pedidoEntrada.Total);
        comandoPedido.Parameters.AddWithValue("@status", "Recebido");
        comandoPedido.Parameters.AddWithValue("@idCliente", idCliente);
        comandoPedido.Parameters.AddWithValue("@idFuncionario", 1);

        var idPedido = Convert.ToInt32(comandoPedido.ExecuteScalar());

        foreach (var item in pedidoEntrada.Itens)
        {
            InserirItemPedido(conexao, transacao, idPedido, item);
        }

        transacao.Commit();

        return new Pedido(
            idPedido,
            pedidoEntrada.ClienteNome,
            pedidoEntrada.ClienteEmail,
            pedidoEntrada.Endereco,
            pedidoEntrada.FormaPagamento,
            pedidoEntrada.Itens,
            pedidoEntrada.Total,
            DateTime.Now,
            "Recebido");
    }
    catch
    {
        transacao.Rollback();
        throw;
    }
}

static Chamado InserirChamado(string conexaoBanco, ChamadoEntrada chamadoEntrada)
{
    using var conexao = new MySqlConnection(conexaoBanco);
    conexao.Open();

    using var comando = new MySqlCommand(
        """
        INSERT INTO chamados
        (nome_cliente, email, telefone, unidade, assunto, mensagem, status, data_hora)
        VALUES
        (@nome, @email, @telefone, @unidade, @assunto, @mensagem, @status, NOW());
        SELECT LAST_INSERT_ID();
        """,
        conexao);

    comando.Parameters.AddWithValue("@nome", chamadoEntrada.ClienteNome);
    comando.Parameters.AddWithValue("@email", chamadoEntrada.ClienteEmail);
    comando.Parameters.AddWithValue("@telefone", chamadoEntrada.ClienteTelefone);
    comando.Parameters.AddWithValue("@unidade", chamadoEntrada.Unidade);
    comando.Parameters.AddWithValue("@assunto", chamadoEntrada.Assunto);
    comando.Parameters.AddWithValue("@mensagem", chamadoEntrada.Descricao);
    comando.Parameters.AddWithValue("@status", "Aberto");

    var idChamado = Convert.ToInt32(comando.ExecuteScalar());

    return new Chamado(
        idChamado,
        chamadoEntrada.Assunto,
        chamadoEntrada.Descricao,
        "Aberto",
        chamadoEntrada.ClienteEmail,
        chamadoEntrada.ClienteNome,
        chamadoEntrada.ClienteTelefone,
        chamadoEntrada.Unidade,
        DateTime.Now);
}

static int InserirCliente(MySqlConnection conexao, MySqlTransaction transacao, string nome, string email)
{
    using var comandoCliente = new MySqlCommand(
        """
        INSERT INTO clientes (nome, email, telefone, senha, data_cadastro)
        VALUES (@nome, @email, '', '', CURDATE());
        SELECT LAST_INSERT_ID();
        """,
        conexao,
        transacao);

    comandoCliente.Parameters.AddWithValue("@nome", nome);
    comandoCliente.Parameters.AddWithValue("@email", email);

    return Convert.ToInt32(comandoCliente.ExecuteScalar());
}

static void InserirItemPedido(MySqlConnection conexao, MySqlTransaction transacao, int idPedido, ItemPedido item)
{
    var tipoItem = item.Nome.StartsWith("Pizza", StringComparison.OrdinalIgnoreCase) ? "Pizza" : "Bebida";
    var valorUnitario = item.Quantidade <= 0 ? item.Valor : item.Valor / item.Quantidade;

    using var comandoItem = new MySqlCommand(
        """
        INSERT INTO itens_pedido
        (tipo_item, nome_item, quantidade, valor_unitario, subtotal, id_pedido)
        VALUES
        (@tipo, @nome, @quantidade, @valorUnitario, @subtotal, @idPedido);
        SELECT LAST_INSERT_ID();
        """,
        conexao,
        transacao);

    comandoItem.Parameters.AddWithValue("@tipo", tipoItem);
    comandoItem.Parameters.AddWithValue("@nome", item.Nome);
    comandoItem.Parameters.AddWithValue("@quantidade", item.Quantidade);
    comandoItem.Parameters.AddWithValue("@valorUnitario", valorUnitario);
    comandoItem.Parameters.AddWithValue("@subtotal", item.Valor);
    comandoItem.Parameters.AddWithValue("@idPedido", idPedido);

    var idItem = Convert.ToInt32(comandoItem.ExecuteScalar());

    if (tipoItem == "Pizza")
    {
        var detalhesPizza = ExtrairDetalhesPizza(item.Nome);
        using var comandoPizza = new MySqlCommand(
            """
            INSERT INTO pizzas
            (tamanho, quantidade_fatias, sabores, borda, valor_da_pizza, valor_da_borda, id_item)
            VALUES
            (@tamanho, @fatias, @sabores, @borda, @valorPizza, @valorBorda, @idItem);
            """,
            conexao,
            transacao);

        comandoPizza.Parameters.AddWithValue("@tamanho", detalhesPizza.Tamanho);
        comandoPizza.Parameters.AddWithValue("@fatias", detalhesPizza.QuantidadeFatias);
        comandoPizza.Parameters.AddWithValue("@sabores", detalhesPizza.Sabores);
        comandoPizza.Parameters.AddWithValue("@borda", detalhesPizza.Borda);
        comandoPizza.Parameters.AddWithValue("@valorPizza", detalhesPizza.ValorPizza);
        comandoPizza.Parameters.AddWithValue("@valorBorda", detalhesPizza.ValorBorda);
        comandoPizza.Parameters.AddWithValue("@idItem", idItem);
        comandoPizza.ExecuteNonQuery();
    }
    else
    {
        using var comandoBebida = new MySqlCommand(
            """
            INSERT INTO bebidas
            (nome_bebida, quantidade, valor, id_item)
            VALUES
            (@nome, @quantidade, @valor, @idItem);
            """,
            conexao,
            transacao);

        comandoBebida.Parameters.AddWithValue("@nome", item.Nome);
        comandoBebida.Parameters.AddWithValue("@quantidade", item.Quantidade);
        comandoBebida.Parameters.AddWithValue("@valor", item.Valor);
        comandoBebida.Parameters.AddWithValue("@idItem", idItem);
        comandoBebida.ExecuteNonQuery();
    }
}

static void AtualizarStatusPedido(string conexaoBanco, int idPedido, string status)
{
    using var conexao = new MySqlConnection(conexaoBanco);
    conexao.Open();

    using var comando = new MySqlCommand(
        "UPDATE pedidos SET status_pedido = @status WHERE id_pedido = @idPedido;",
        conexao);

    comando.Parameters.AddWithValue("@status", status);
    comando.Parameters.AddWithValue("@idPedido", idPedido);
    comando.ExecuteNonQuery();
}

static void AtualizarStatusChamado(string conexaoBanco, int idChamado, string status)
{
    using var conexao = new MySqlConnection(conexaoBanco);
    conexao.Open();

    using var comando = new MySqlCommand(
        "UPDATE chamados SET status = @status WHERE id_chamado = @idChamado;",
        conexao);

    comando.Parameters.AddWithValue("@status", status);
    comando.Parameters.AddWithValue("@idChamado", idChamado);
    comando.ExecuteNonQuery();
}

static DetalhesPizza ExtrairDetalhesPizza(string nome)
{
    if (!nome.Contains('(') || !nome.Contains(')'))
    {
        return new DetalhesPizza("", 0, nome.Replace("Pizza", "").Trim(), "Sem borda", 0, 0);
    }

    var tamanho = nome.Split('(', 2)[0].Replace("Pizza", "").Trim();
    var sabores = nome.Split('(', 2)[1].Split(')', 2)[0].Trim();
    var borda = nome.Contains(" - ") ? nome.Split(" - ", 2)[1].Trim() : "Sem borda";
    var fatias = tamanho.Equals("Média", StringComparison.OrdinalIgnoreCase) || tamanho.Equals("Média", StringComparison.OrdinalIgnoreCase) ? 6 :
        tamanho.Equals("Família", StringComparison.OrdinalIgnoreCase) || tamanho.Equals("Família", StringComparison.OrdinalIgnoreCase) ? 12 :
        tamanho.Equals("Pequena", StringComparison.OrdinalIgnoreCase) || tamanho.Equals("Broto", StringComparison.OrdinalIgnoreCase) ? 4 :
        tamanho.Equals("Grande", StringComparison.OrdinalIgnoreCase) ? 8 : 0;
    var valorPizza = tamanho.Equals("Pequena", StringComparison.OrdinalIgnoreCase) || tamanho.Equals("Broto", StringComparison.OrdinalIgnoreCase) ? 34.90m :
        tamanho.Equals("Média", StringComparison.OrdinalIgnoreCase) || tamanho.Equals("Média", StringComparison.OrdinalIgnoreCase) ? 39.90m :
        tamanho.Equals("Família", StringComparison.OrdinalIgnoreCase) || tamanho.Equals("Família", StringComparison.OrdinalIgnoreCase) ? 69.90m :
        tamanho.Equals("Grande", StringComparison.OrdinalIgnoreCase) ? 49.90m : 0;
    var valorBorda = borda.Equals("Catupiry", StringComparison.OrdinalIgnoreCase) ? 8.90m :
        borda.Equals("Cheddar", StringComparison.OrdinalIgnoreCase) ? 10.90m :
        borda.Equals("Chocolate", StringComparison.OrdinalIgnoreCase) ? 12.90m : 0;

    return new DetalhesPizza(tamanho, fatias, sabores, borda, valorPizza, valorBorda);
}

static string LerTexto(MySqlDataReader leitor, string coluna)
{
    var indice = leitor.GetOrdinal(coluna);
    return leitor.IsDBNull(indice) ? "" : leitor.GetString(indice);
}

static decimal LerDecimal(MySqlDataReader leitor, string coluna)
{
    var indice = leitor.GetOrdinal(coluna);
    return leitor.IsDBNull(indice) ? 0 : leitor.GetDecimal(indice);
}

static int LerInteiro(MySqlDataReader leitor, string coluna)
{
    var indice = leitor.GetOrdinal(coluna);
    return leitor.IsDBNull(indice) ? 0 : leitor.GetInt32(indice);
}

static DateTime LerData(MySqlDataReader leitor, string coluna)
{
    var indice = leitor.GetOrdinal(coluna);
    return leitor.IsDBNull(indice) ? DateTime.Now : leitor.GetDateTime(indice);
}

record Funcionario(string Nome, string Senha, string Cargo);

record PedidoEntrada(
    string ClienteNome,
    string ClienteEmail,
    string Endereco,
    string FormaPagamento,
    List<ItemPedido> Itens,
    decimal Total);

record ItemPedido(string Nome, int Quantidade, decimal Valor);

record DetalhesPizza(string Tamanho, int QuantidadeFatias, string Sabores, string Borda, decimal ValorPizza, decimal ValorBorda);

record ChamadoEntrada(
    string ClienteNome,
    string ClienteEmail,
    string ClienteTelefone,
    string Unidade,
    string Assunto,
    string Descricao);

class Pedido
{
    public Pedido()
    {
    }

    public Pedido(
        int id,
        string clienteNome,
        string clienteEmail,
        string endereco,
        string formaPagamento,
        List<ItemPedido> itens,
        decimal total,
        DateTime dataHora,
        string status)
    {
        Id = id;
        ClienteNome = clienteNome;
        ClienteEmail = clienteEmail;
        Endereco = endereco;
        FormaPagamento = formaPagamento;
        Itens = itens;
        Total = total;
        DataHora = dataHora;
        Status = status;
    }

    public int Id { get; set; }
    public string ClienteNome { get; set; } = "";
    public string ClienteEmail { get; set; } = "";
    public string Endereco { get; set; } = "";
    public string FormaPagamento { get; set; } = "";
    public List<ItemPedido> Itens { get; set; } = new List<ItemPedido>();
    public decimal Total { get; set; }
    public DateTime DataHora { get; set; }
    public string Status { get; set; } = "";
}

class Chamado
{
    public Chamado()
    {
    }

    public Chamado(int id, string assunto, string descricao, string status, string cliente)
        : this(id, assunto, descricao, status, cliente, cliente, "", "", DateTime.Now)
    {
    }

    public Chamado(
        int id,
        string assunto,
        string descricao,
        string status,
        string cliente,
        string clienteNome,
        string clienteTelefone,
        string unidade,
        DateTime dataHora)
    {
        Id = id;
        Assunto = assunto;
        Descricao = descricao;
        Status = status;
        Cliente = cliente;
        ClienteNome = clienteNome;
        ClienteTelefone = clienteTelefone;
        Unidade = unidade;
        DataHora = dataHora;
    }

    public int Id { get; set; }
    public string Assunto { get; set; } = "";
    public string Descricao { get; set; } = "";
    public string Cliente { get; set; } = "";
    public string ClienteNome { get; set; } = "";
    public string ClienteTelefone { get; set; } = "";
    public string Unidade { get; set; } = "";
    public DateTime DataHora { get; set; }
    public string Status { get; set; } = "";
}



