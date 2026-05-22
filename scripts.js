var carrinho = {};
var enderecoApiPedidos = "http://localhost:5000/api/pedidos";
var enderecoApiChamados = "http://localhost:5000/api/chamados";
var modoAutenticacao = "login";
var tempoAvisoPedido;

function iniciarAutenticacao() {
  document
    .getElementById("formulario-autenticacao")
    .addEventListener("submit", enviarAutenticacao);
  document.getElementById("aba-login").addEventListener("click", function () {
    definirModoAutenticacao("login");
  });
  document
    .getElementById("aba-cadastro")
    .addEventListener("click", function () {
      definirModoAutenticacao("cadastro");
    });
  document
    .getElementById("botao-recuperar-senha")
    .addEventListener("click", function () {
      definirModoAutenticacao("recuperacao");
    });
  iniciarFormularioChamado();

  var usuario = buscarUsuarioLogado();
  if (usuario) {
    atualizarTelaUsuario(usuario);
    return;
  }

  mostrarModalAutenticacao();
}

function mostrarModalAutenticacao() {
  document.getElementById("modal-autenticacao").style.display = "flex";
  document.body.style.overflow = "hidden";
}

function definirModoAutenticacao(modo) {
  modoAutenticacao = modo;
  document
    .getElementById("aba-login")
    .classList.toggle("ativo", modo === "login");
  document
    .getElementById("aba-cadastro")
    .classList.toggle("ativo", modo === "cadastro");
  document.getElementById("campos-cadastro").style.display =
    modo === "cadastro" ? "block" : "none";
  document.getElementById("campos-recuperacao").style.display =
    modo === "recuperacao" ? "block" : "none";
  document.getElementById("botao-recuperar-senha").style.display =
    modo === "login" ? "inline-block" : "none";
  document.querySelector('label[for="senha-autenticacao"]').textContent =
    modo === "recuperacao" ? "Nova senha" : "Senha";
  document.getElementById("botao-autenticacao").textContent =
    modo === "cadastro"
      ? "Cadastrar"
      : modo === "recuperacao"
        ? "Alterar senha"
        : "Entrar";
  document.getElementById("mensagem-autenticacao").textContent = "";
}

function enviarAutenticacao(evento) {
  evento.preventDefault();

  var email = document
    .getElementById("email-autenticacao")
    .value.trim()
    .toLowerCase();
  var senha = document.getElementById("senha-autenticacao").value.trim();
  var nome = document.getElementById("nome-autenticacao").value.trim();
  var confirmarSenha = document
    .getElementById("confirmar-senha-autenticacao")
    .value.trim();

  if (!email || !senha) {
    mostrarMensagemAutenticacao("Preencha email e senha.");
    return;
  }

  if (modoAutenticacao === "cadastro") {
    if (!nome) {
      mostrarMensagemAutenticacao("Informe seu nome para cadastro.");
      return;
    }

    if (senha.length < 6) {
      mostrarMensagemAutenticacao("A senha deve ter pelo menos 6 caracteres.");
      return;
    }

    cadastrarUsuario(nome, email, senha);
    return;
  }

  if (modoAutenticacao === "recuperacao") {
    redefinirSenhaUsuario(email, senha, confirmarSenha);
    return;
  }

  entrarUsuario(email, senha);
}

function cadastrarUsuario(nome, email, senha) {
  var usuarios = buscarUsuariosSalvos();
  if (usuarios[email]) {
    mostrarMensagemAutenticacao("Email ja cadastrado. Use login.");
    return;
  }

  usuarios[email] = { nome: nome, email: email, senha: senha, chamados: [] };
  localStorage.setItem("napoliUsers", JSON.stringify(usuarios));
  localStorage.setItem("napoliUser", JSON.stringify(usuarios[email]));
  atualizarTelaUsuario(usuarios[email]);
}

function entrarUsuario(email, senha) {
  var usuarios = buscarUsuariosSalvos();
  var usuario = usuarios[email];

  if (!usuario || (usuario.senha || usuario.password) !== senha) {
    mostrarMensagemAutenticacao("Email ou senha incorretos.");
    return;
  }

  usuario = garantirDadosUsuario(usuario);
  salvarUsuarioAtual(usuario);
  atualizarTelaUsuario(usuario);
}

function redefinirSenhaUsuario(email, senha, confirmarSenha) {
  var usuarios = buscarUsuariosSalvos();
  var usuario = usuarios[email];

  if (!usuario) {
    mostrarMensagemAutenticacao("Email nao encontrado. Faca seu cadastro.");
    return;
  }

  if (senha.length < 6) {
    mostrarMensagemAutenticacao(
      "A nova senha deve ter pelo menos 6 caracteres.",
    );
    return;
  }

  if (senha !== confirmarSenha) {
    mostrarMensagemAutenticacao("As senhas digitadas nao conferem.");
    return;
  }

  usuario = garantirDadosUsuario(usuario);
  usuario.senha = senha;
  delete usuario.password;
  usuarios[email] = usuario;
  localStorage.setItem("napoliUsers", JSON.stringify(usuarios));
  document.getElementById("senha-autenticacao").value = "";
  document.getElementById("confirmar-senha-autenticacao").value = "";
  definirModoAutenticacao("login");
  mostrarMensagemAutenticacao("Senha alterada. Entre com sua nova senha.");
}

function atualizarTelaUsuario(usuario) {
  usuario = garantirDadosUsuario(usuario);
  salvarUsuarioAtual(usuario);

  document.getElementById("modal-autenticacao").style.display = "none";
  document.body.style.overflow = "auto";
  document.getElementById("saudacao-usuario").textContent =
    "Ola, " + usuario.nome;
  document.getElementById("botao-sair").style.display = "inline-block";
  mostrarChamados();
}

function sairUsuario() {
  localStorage.removeItem("napoliUser");
  document.getElementById("saudacao-usuario").textContent = "";
  document.getElementById("botao-sair").style.display = "none";
  definirModoAutenticacao("login");
  document.getElementById("email-autenticacao").value = "";
  document.getElementById("senha-autenticacao").value = "";
  document.getElementById("nome-autenticacao").value = "";
  document.getElementById("confirmar-senha-autenticacao").value = "";
  mostrarChamados();
  mostrarModalAutenticacao();
}

function mostrarMensagemAutenticacao(mensagem) {
  document.getElementById("mensagem-autenticacao").textContent = mensagem;
}

function buscarUsuarioLogado() {
  var usuario = localStorage.getItem("napoliUser");
  return usuario ? JSON.parse(usuario) : null;
}

function buscarUsuariosSalvos() {
  return JSON.parse(localStorage.getItem("napoliUsers") || "{}");
}

function garantirDadosUsuario(usuario) {
  if (usuario.name && !usuario.nome) {
    usuario.nome = usuario.name;
  }

  if (usuario.password && !usuario.senha) {
    usuario.senha = usuario.password;
  }

  if (!usuario.chamados) {
    usuario.chamados = [];
  }

  return usuario;
}

function salvarUsuarioAtual(usuario) {
  var usuarios = buscarUsuariosSalvos();
  usuarios[usuario.email] = usuario;
  localStorage.setItem("napoliUsers", JSON.stringify(usuarios));
  localStorage.setItem("napoliUser", JSON.stringify(usuario));
}

function iniciarFormularioChamado() {
  var formularioChamado = document.getElementById("formulario-chamado");
  if (!formularioChamado) {
    return;
  }

  formularioChamado.addEventListener("submit", enviarChamado);
  mostrarChamados();
}

async function enviarChamado(evento) {
  evento.preventDefault();

  var usuario = buscarUsuarioLogado();
  if (!usuario) {
    mostrarMensagemChamado("Faca login para abrir um chamado.");
    mostrarModalAutenticacao();
    return;
  }

  var assunto = document.getElementById("assunto-chamado").value.trim();
  var descricao = document.getElementById("descricao-chamado").value.trim();
  var nomeContato = document.getElementById("nome-chamado").value.trim();
  var emailContato = document.getElementById("email-chamado").value.trim();
  var telefoneContato = document
    .getElementById("telefone-chamado")
    .value.trim();
  var unidadeContato = document.getElementById("unidade-chamado").value;

  if (!assunto || !descricao) {
    mostrarMensagemChamado("Preencha o tipo de contato e a mensagem.");
    return;
  }

  usuario = garantirDadosUsuario(usuario);

  var descricaoCompleta =
    "Nome: " +
    (nomeContato || usuario.nome) +
    "\nEmail: " +
    (emailContato || usuario.email) +
    "\nTelefone: " +
    (telefoneContato || "Nao informado") +
    "\nUnidade: " +
    unidadeContato +
    "\n\nMensagem: " +
    descricao;

  var chamado = {
    id: Date.now(),
    assunto: assunto,
    descricao: descricaoCompleta,
    status: "Aberto",
    data: new Date().toLocaleString("pt-BR"),
  };

  usuario.chamados.unshift(chamado);

  salvarUsuarioAtual(usuario);
  evento.target.reset();
  mostrarChamados();

  try {
    var resposta = await fetch(enderecoApiChamados, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        clienteNome: nomeContato || usuario.nome,
        clienteEmail: emailContato || usuario.email,
        clienteTelefone: telefoneContato,
        unidade: unidadeContato,
        assunto: assunto,
        descricao: descricao,
      }),
    });

    if (!resposta.ok) {
      throw new Error("Erro ao enviar chamado.");
    }

    mostrarMensagemChamado("Chamado aberto e enviado ao painel.");
  } catch (erro) {
    mostrarMensagemChamado(
      "Chamado aberto com sucesso! Nossa equipe entrara em contato em breve.",
    );
  }
}

function mostrarChamados() {
  var listaChamados = document.getElementById("lista-chamados");
  if (!listaChamados) {
    return;
  }

  var usuario = buscarUsuarioLogado();
  if (!usuario) {
    listaChamados.innerHTML =
      '<p class="chamados-vazio">Faca login para visualizar seus chamados.</p>';
    return;
  }

  usuario = garantirDadosUsuario(usuario);
  if (usuario.chamados.length === 0) {
    listaChamados.innerHTML =
      '<p class="chamados-vazio">Nenhum chamado aberto ainda.</p>';
    return;
  }

  listaChamados.innerHTML = "";
  usuario.chamados.slice(0, 5).forEach(function (chamado) {
    var itemChamado = document.createElement("div");
    itemChamado.className = "item-chamado";

    var cabecalhoChamado = document.createElement("div");
    cabecalhoChamado.className = "cabecalho-chamado";

    var tituloChamado = document.createElement("h5");
    tituloChamado.textContent = chamado.assunto;

    var statusChamado = document.createElement("span");
    statusChamado.className = "status-chamado";
    statusChamado.textContent = chamado.status;

    var descricaoChamado = document.createElement("p");
    descricaoChamado.textContent = chamado.descricao;

    var dataChamado = document.createElement("small");
    dataChamado.textContent = chamado.data;

    cabecalhoChamado.appendChild(tituloChamado);
    cabecalhoChamado.appendChild(statusChamado);
    itemChamado.appendChild(cabecalhoChamado);
    itemChamado.appendChild(descricaoChamado);
    itemChamado.appendChild(dataChamado);
    listaChamados.appendChild(itemChamado);
  });
}

function mostrarMensagemChamado(mensagem) {
  document.getElementById("mensagem-chamado").textContent = mensagem;
}

function iniciarMontadorPizza() {
  var tamanhos = document.querySelectorAll('input[name="tamanho-pizza"]');
  var sabores = document.querySelectorAll(".sabores-pizza input");
  var borda = document.getElementById("borda-pizza");

  if (!tamanhos.length || !sabores.length || !borda) {
    return;
  }

  tamanhos.forEach(function (tamanho) {
    tamanho.addEventListener("change", function () {
      limitarSaboresPizza();
      atualizarResumoPizza();
    });
  });

  sabores.forEach(function (sabor) {
    sabor.addEventListener("change", function () {
      limitarSaboresPizza();
      atualizarResumoPizza();
    });
  });

  borda.addEventListener("change", atualizarResumoPizza);
  atualizarResumoPizza();
}

function buscarTamanhoPizzaSelecionado() {
  return document.querySelector('input[name="tamanho-pizza"]:checked');
}

function buscarSaboresPizzaSelecionados() {
  return Array.from(
    document.querySelectorAll(".sabores-pizza input:checked"),
  ).map(function (sabor) {
    return sabor.value;
  });
}

function limitarSaboresPizza() {
  var tamanho = buscarTamanhoPizzaSelecionado();
  if (!tamanho) {
    return;
  }

  var limite = Number(tamanho.dataset.sabores);
  var sabores = document.querySelectorAll(".sabores-pizza input");
  var escolhidos = buscarSaboresPizzaSelecionados();
  var mensagem = document.getElementById("mensagem-monte-pizza");

  if (escolhidos.length > limite) {
    for (var indice = sabores.length - 1; indice >= 0; indice -= 1) {
      if (sabores[indice].checked) {
        sabores[indice].checked = false;
        break;
      }
    }
    mensagem.textContent = "Esse tamanho permite ate " + limite + " sabor(es).";
  } else {
    mensagem.textContent = "";
  }

  escolhidos = buscarSaboresPizzaSelecionados();
  sabores.forEach(function (sabor) {
    sabor.parentElement.classList.toggle("escolhido", sabor.checked);
    sabor.disabled = escolhidos.length >= limite && !sabor.checked;
  });
}

function atualizarResumoPizza() {
  var tamanho = buscarTamanhoPizzaSelecionado();
  var borda = document.getElementById("borda-pizza");
  if (!tamanho || !borda) {
    return;
  }

  var sabores = buscarSaboresPizzaSelecionados();
  var limite = Number(tamanho.dataset.sabores);
  var precoBase = Number(tamanho.dataset.preco);
  var precoBorda = Number(borda.options[borda.selectedIndex].dataset.preco);
  var total = precoBase + precoBorda;

  document.getElementById("limite-sabores-pizza").textContent =
    "Escolha ate " + limite + " sabor(es).";
  document.getElementById("resumo-pizza").textContent =
    tamanho.value + " · " + tamanho.dataset.fatias + " fatias";
  document.getElementById("resumo-sabores-pizza").textContent =
    sabores.length > 0 ? sabores.join(" + ") : "Nenhum sabor escolhido";
  document.getElementById("resumo-borda-pizza").textContent = borda.value;
  document.getElementById("total-pizza").textContent =
    "R$ " + total.toFixed(2).replace(".", ",");
}

function adicionarPizzaMontada() {
  var tamanho = buscarTamanhoPizzaSelecionado();
  var borda = document.getElementById("borda-pizza");
  var mensagem = document.getElementById("mensagem-monte-pizza");
  var sabores = buscarSaboresPizzaSelecionados();

  if (!tamanho || !borda || sabores.length === 0) {
    mensagem.textContent = "Escolha pelo menos um sabor para montar sua pizza.";
    return;
  }

  var precoBase = Number(tamanho.dataset.preco);
  var precoBorda = Number(borda.options[borda.selectedIndex].dataset.preco);
  var total = precoBase + precoBorda;
  var nomePizza =
    "Pizza " +
    tamanho.value +
    " (" +
    sabores.join(" + ") +
    ") - " +
    borda.value;

  adicionarAoCarrinho("pizza-montada-" + Date.now(), total, nomePizza);
  mensagem.textContent = "Pizza adicionada ao carrinho.";
}

function mostrarPizzas() {
  document.getElementById("lista-bebidas").style.display = "none";
  document.getElementById("menu-bebida").style.backgroundColor = "#ecf9fc";
  document.getElementById("menu-pizza").style.backgroundColor = "#00C5A1";
  document.getElementById("lista-pizzas").style.display = "flex";
}

function mostrarBebidas() {
  document.getElementById("lista-pizzas").style.display = "none";
  document.getElementById("lista-bebidas").style.display = "flex";
  document.getElementById("menu-pizza").style.backgroundColor = "#ecf9fc";
  document.getElementById("menu-bebida").style.backgroundColor = "#00C5A1";
}

function adicionarAoCarrinho(tipoItem, valorItem, nomeItem) {
  if (carrinho.total === undefined) {
    carrinho.total = {
      valor: valorItem,
    };
  } else {
    carrinho.total.valor = parseFloat(
      (carrinho.total.valor + valorItem).toFixed(2),
    );
  }

  if (carrinho[tipoItem] === undefined) {
    carrinho[tipoItem] = {
      tipo: tipoItem,
      quantidade: 1,
      valor: valorItem,
      nome: nomeItem,
    };
  } else {
    carrinho[tipoItem].quantidade += 1;
    carrinho[tipoItem].valor = parseFloat(
      (carrinho[tipoItem].valor + valorItem).toFixed(2),
    );
  }

  atualizarCarrinho();
}

function removerDoCarrinho(tipoItem) {
  if (!carrinho[tipoItem]) {
    return;
  }

  carrinho.total.valor = parseFloat(
    (carrinho.total.valor - carrinho[tipoItem].valor).toFixed(2),
  );
  delete carrinho[tipoItem];

  if (carrinho.total.valor <= 0) {
    delete carrinho.total;
  }

  atualizarCarrinho();
}

function atualizarCarrinho() {
  var carrinhoTela = document.getElementById("carrinho-flutuante");
  var itensCarrinho = document.getElementById("itens-carrinho");
  var totalCarrinho = document.getElementById("total-carrinho");
  var botaoFinalizar = document.getElementById("finalizar-pedido");
  var mensagemPedido = document.getElementById("mensagem-pedido");

  itensCarrinho.innerHTML = "";
  mensagemPedido.textContent = "";
  mensagemPedido.classList.remove("sucesso", "erro");
  var quantidadeItens = 0;

  Object.values(carrinho).forEach(function (item) {
    if (item.tipo !== undefined) {
      quantidadeItens += 1;

      var linha = document.createElement("div");
      linha.className = "linha-carrinho";
      linha.innerHTML =
        '<div class="texto-linha-carrinho"><span>' +
        item.quantidade +
        "x " +
        item.nome +
        "</span><span>R$ " +
        item.valor.toFixed(2) +
        "</span></div>" +
        '<button type="button" class="remover-carrinho" onclick="removerDoCarrinho(\'' +
        item.tipo +
        "')\">Excluir</button>";
      itensCarrinho.appendChild(linha);
    }
  });

  totalCarrinho.textContent =
    "R$ " + (carrinho.total ? carrinho.total.valor.toFixed(2) : "0.00");
  botaoFinalizar.disabled = !carrinho.total;
  carrinhoTela.style.display = quantidadeItens > 0 ? "flex" : "none";
}

function mostrarAvisoPedido(mensagem, tipo) {
  var aviso = document.getElementById("aviso");
  var descricaoAviso = document.getElementById("descricao-aviso");

  if (!aviso || !descricaoAviso) {
    return;
  }

  clearTimeout(tempoAvisoPedido);
  descricaoAviso.textContent = mensagem;
  aviso.classList.remove("erro");

  if (tipo === "erro") {
    aviso.classList.add("erro");
  }

  aviso.classList.remove("visivel");
  void aviso.offsetWidth;
  aviso.classList.add("visivel");

  tempoAvisoPedido = setTimeout(function () {
    aviso.classList.remove("visivel", "erro");
  }, 20000);
}

async function finalizarPedido() {
  var usuario = buscarUsuarioLogado();
  var endereco = document.getElementById("endereco-pedido").value.trim();
  var formaPagamento = document.getElementById("pagamento-pedido").value;
  var mensagemPedido = document.getElementById("mensagem-pedido");
  var botaoFinalizar = document.getElementById("finalizar-pedido");

  if (!usuario) {
    mensagemPedido.textContent = "Faca login para finalizar o pedido.";
    mostrarModalAutenticacao();
    return;
  }

  if (!endereco || !formaPagamento) {
    mensagemPedido.textContent = "Informe o endereco e a forma de pagamento.";
    return;
  }

  var itensPedido = Object.values(carrinho)
    .filter(function (item) {
      return item.tipo !== undefined;
    })
    .map(function (item) {
      return {
        nome: item.nome,
        quantidade: item.quantidade,
        valor: item.valor,
      };
    });

  if (!carrinho.total || itensPedido.length === 0) {
    mensagemPedido.textContent =
      "Adicione um item ao carrinho antes de finalizar.";
    return;
  }

  var pedido = {
    clienteNome: usuario.nome,
    clienteEmail: usuario.email,
    endereco: endereco,
    formaPagamento: formaPagamento,
    itens: itensPedido,
    total: carrinho.total.valor,
  };

  botaoFinalizar.disabled = true;
  mensagemPedido.textContent = "Enviando pedido para a pizzaria...";
  mensagemPedido.classList.remove("sucesso", "erro");

  try {
    var resposta = await fetch(enderecoApiPedidos, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(pedido),
    });

    if (!resposta.ok) {
      throw new Error("Erro ao enviar pedido.");
    }
  } catch (erro) {
    var mensagemErro =
      "Não foi possível enviar seu pedido no momento. Tente novamente em instantes.";
    mensagemPedido.textContent = mensagemErro;
    mensagemPedido.classList.add("erro");
    mostrarAvisoPedido(mensagemErro, "erro");
    botaoFinalizar.disabled = false;
    return;
  }

  mostrarAvisoPedido("Pedido realizado com sucesso!", "sucesso");
  mensagemPedido.textContent = "Pedido realizado com sucesso!";
  mensagemPedido.classList.remove("erro");
  mensagemPedido.classList.add("sucesso");

  setTimeout(function () {
    document.getElementById("carrinho-flutuante").style.display = "none";
    document.getElementById("itens-carrinho").innerHTML = "";
    document.getElementById("total-carrinho").textContent = "R$ 0,00";
    document.getElementById("endereco-pedido").value = "";
    document.getElementById("pagamento-pedido").value = "";
    mensagemPedido.textContent = "";
    mensagemPedido.classList.remove("sucesso", "erro");
    botaoFinalizar.disabled = true;

    carrinho = {};
  }, 4000);
}

document.addEventListener("DOMContentLoaded", iniciarAutenticacao);
document.addEventListener("DOMContentLoaded", iniciarMontadorPizza);

