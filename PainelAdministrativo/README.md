# Painel Administrativo Napoli Express

Painel simples em C# com ASP.NET Core para uso interno dos funcionarios da pizzaria.

## Como executar

```bash
dotnet run --urls "http://localhost:5000"
```

Depois, abra o endereco exibido no terminal.

Para testar junto com o site, mantenha o painel em `http://localhost:5000`.
Os pedidos enviados pelo carrinho entram pela rota `/api/pedidos` e aparecem
para os funcionarios na pagina `/pedidos`.

## Acesso de apresentacao

- E-mail: `admin@napolipizzaria.com.br`
- Senha: `admin123`

Tambem existe o usuario:

- E-mail: `atendimento@napolipizzaria.com.br`
- Senha: `pizza123`

## Banco de dados MySQL

O painel grava pedidos e chamados no banco `napoli_express_pizzaria`.
Antes de executar, deixe o MySQL ligado e crie o banco com o script:

```text
../napoli_express_pizzaria.sql
```

Por padrao, a conexao usada pelo C# e:

```text
Server=localhost;Port=3306;Database=napoli_express_pizzaria;User ID=root;Password=;
```

Se o seu MySQL tiver senha, altere a variavel de ambiente `MYSQL_CONNECTION_STRING`
antes de rodar o projeto.
