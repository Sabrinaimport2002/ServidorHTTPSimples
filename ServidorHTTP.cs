using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web;

class ServidorHttp
{
    private TcpListener Controlador { get; set; }
    private int Porta { get; set; }
    private int QtdeRequests { get; set; }
    public string HtmlExemplo { get; set; }
    private SortedList<string, string> TiposMime { get; set; }
    private SortedList<string, string> DiretoriosHosts { get; set; }

    public ServidorHttp(int porta = 8080)
    {
        Porta = porta;
        CriarHtmlExemplo();
        PopularTiposMIME();
        PopularDiretoriosHosts();
        try
        {
            Controlador = new TcpListener(IPAddress.Parse("127.0.0.1"), Porta);
            Controlador.Start();
            Console.WriteLine($"Servidor HTTP está rodando na porta {Porta}.");
            Console.WriteLine($"Para acessar, digite no navegador: http://localhost:{Porta}.");
            Task servidorHttpTask = Task.Run(() => AguardarRequests());
            servidorHttpTask.GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Erro ao iniciar servidor na porta {Porta}:\n{e.Message}");
        }
    }

    private async Task AguardarRequests()
    {
        while (true)
        {
            Socket conexao = await Controlador.AcceptSocketAsync();
            QtdeRequests++;
            Task task = Task.Run(() => ProcessarRequest(conexao, QtdeRequests));
        }
    }

    private void ProcessarRequest(Socket conexao, int numeroRequest)
    {
        Console.WriteLine($"Processando request #{numeroRequest}...\n");
        if (conexao.Connected)
        {
            byte[] bytesRequisicao = new byte[1024];
            conexao.Receive(bytesRequisicao, bytesRequisicao.Length, 0);
            string textoRequisicao = Encoding.UTF8.GetString(bytesRequisicao)
                .Replace((char)0, ' ').Trim();
            if (textoRequisicao.Length > 0)
            {
                Console.WriteLine($"\n{textoRequisicao}\n");

                string[] linhas = textoRequisicao.Split("\r\n");
                int iPrimeiroEspaco = linhas[0].IndexOf(' ');
                int iSegundoEspaco = linhas[0].LastIndexOf(' ');
                string metodoHttp = linhas[0].Substring(0, iPrimeiroEspaco);
                string recursoBuscado = linhas[0].Substring(
                    iPrimeiroEspaco + 1, iSegundoEspaco - iPrimeiroEspaco - 1);
                if (recursoBuscado == "/") recursoBuscado = "/index.html";
                string textoParametros = recursoBuscado.Contains("?") ?
                    recursoBuscado.Split("?")[1] : "";
                SortedList<string, string> parametros = ProcessarParametros(textoParametros);
                string dadosPost = textoRequisicao.Contains("\r\n\r\n") ?
                    textoRequisicao.Split("\r\n\r\n")[1] : "";
                if (!string.IsNullOrEmpty(dadosPost))
                {
                    dadosPost = HttpUtility.UrlDecode(dadosPost, Encoding.UTF8);
                    var parametrosPost = ProcessarParametros(dadosPost);
                    foreach (var pp in parametrosPost)
                        parametros.Add(pp.Key, pp.Value);
                }
                recursoBuscado = recursoBuscado.Split("?")[0];
                string versaoHttp = linhas[0].Substring(iSegundoEspaco + 1);
                iPrimeiroEspaco = linhas[1].IndexOf(' ');
                string nomeHost = linhas[1].Substring(iPrimeiroEspaco + 1);

                byte[] bytesCabecalho = null;
                byte[] bytesConteudo = null;
                FileInfo fiArquivo = new FileInfo(ObterCaminhoFisicoArquivo(nomeHost, recursoBuscado));
                if (fiArquivo.Exists)
                {
                    if (TiposMime.ContainsKey(fiArquivo.Extension.ToLower()))
                    {
                        if (fiArquivo.Extension.ToLower() == ".dhtml")
                            bytesConteudo = GerarHTMLDinamico(fiArquivo.FullName, parametros, metodoHttp);
                        else
                            bytesConteudo = File.ReadAllBytes(fiArquivo.FullName);

                        string tipoMime = TiposMime[fiArquivo.Extension.ToLower()];
                        bytesCabecalho = GerarCabecalho(versaoHttp, tipoMime,
                            "200", bytesConteudo.Length);
                    }
                    else
                    {
                        bytesConteudo = Encoding.UTF8.GetBytes(
                            "<h1>Erro 415 - Tipo de arquivo não suportado.</h1>");
                        bytesCabecalho = GerarCabecalho(versaoHttp, "text/html;charset=utf-8",
                            "415", bytesConteudo.Length);
                    }
                }
                else
                {
                    bytesConteudo = Encoding.UTF8.GetBytes(
                        "<h1>Erro 404 - Arquivo Não Encontrado</h1>");
                    bytesCabecalho = GerarCabecalho(versaoHttp, "text/html;charset=utf-8",
                        "404", bytesConteudo.Length);
                }
                int bytesEnviados = conexao.Send(bytesCabecalho, bytesCabecalho.Length, 0);
                bytesEnviados += conexao.Send(bytesConteudo, bytesConteudo.Length, 0);
                conexao.Close();
                Console.WriteLine($"\n{bytesEnviados} bytes enviados em resposta à requisição #{numeroRequest}.");
            }
        }
        Console.WriteLine($"\nRequest {numeroRequest} finalizado.");
    }

    public byte[] GerarCabecalho(string versaoHttp, string tipoMime,
        string codigoHttp, int qtdeBytes = 0)
    {
        StringBuilder texto = new StringBuilder();
        texto.Append($"{versaoHttp} {codigoHttp}{Environment.NewLine}");
        texto.Append($"Server: Servidor Http Simples 1.0{Environment.NewLine}");
        texto.Append($"Content-Type: {tipoMime}{Environment.NewLine}");
        texto.Append($"Content-Length: {qtdeBytes}{Environment.NewLine}{Environment.NewLine}");
        return Encoding.UTF8.GetBytes(texto.ToString());
    }

    private void CriarHtmlExemplo()
    {
        StringBuilder html = new StringBuilder();
        html.Append("<!DOCTYPE html><html lang=\"pt-br\"><head><meta charset=\"UTF-8\">");
        html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.Append("<title>Página Estática</title></head><body>");
        html.Append("<h1>Página Estática</h1></body></html>");
        HtmlExemplo = html.ToString();
    }

    private void PopularTiposMIME()
    {
        TiposMime = new SortedList<string, string>
        {
            { ".html", "text/html;charset=utf-8" },
            { ".htm", "text/html;charset=utf-8" },
            { ".css", "text/css" },
            { ".js", "text/javascript" },
            { ".png", "image/png" },
            { ".jpg", "image/jpeg" },
            { ".gif", "image/gif" },
            { ".svg", "image/svg+xml" },
            { ".webp", "image/webp" },
            { ".ico", "image/ico" },
            { ".woff", "font/woff" },
            { ".woff2", "font/woff2" },
            { ".dhtml", "text/html;charset=utf-8" }
        };
    }

    private void PopularDiretoriosHosts()
    {
        DiretoriosHosts = new SortedList<string, string>
        {
            { "localhost", "C:\\dev\\cursos\\ServidorHTTP\\www\\localhost" },
            { "servidorsiteteste.com", "C:\\dev\\cursos\\ServidorHTTP\\www\\servidorsiteteste.com" },
        };
    }

    public string ObterCaminhoFisicoArquivo(string host, string arquivo)
    {
        string diretorio = DiretoriosHosts[host.Split(":")[0]];
        string caminhoArquivo = diretorio + arquivo.Replace("/", "\\");
        return caminhoArquivo;
    }

    public byte[] GerarHTMLDinamico(string caminhoArquivo, SortedList<string, string> parametros, string metodoHttp)
    {
        FileInfo fiArquivo = new FileInfo(caminhoArquivo);
        string nomeClassePagina = "Pagina" + fiArquivo.Name.Replace(fiArquivo.Extension, "");

        //Instanciar classe a partir do nome
        Type tipoPaginaDinamica = Type.GetType(nomeClassePagina, true, true);
        PaginaDinamica pd = Activator.CreateInstance(tipoPaginaDinamica) as PaginaDinamica;
        pd.HtmlModelo = File.ReadAllText(caminhoArquivo);
        switch (metodoHttp.ToLower())
        {
            case "get":
                return pd.Get(parametros);
            case "post":
                return pd.Post(parametros);
            default:
                return new byte[0];
        }
    }

    private SortedList<string, string> ProcessarParametros(string textoParametros)
    {
        SortedList<string, string> parametros = new SortedList<string, string>();
        //v=Gm2pJfCJyUw&t=1000s
        if (!string.IsNullOrEmpty(textoParametros.Trim()))
        {
            string[] paresChaveValor = textoParametros.Split("&");
            foreach (var par in paresChaveValor)
            {
                parametros.Add(par.Split("=")[0].ToLower(), par.Split("=")[1]);
            }
        }
        return parametros;
    }
}