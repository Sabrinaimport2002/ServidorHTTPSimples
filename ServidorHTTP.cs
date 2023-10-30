using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

class ServidorHttp
{
    private TcpListener Controlador { get; set; }
    private int Porta { get; set; }
    private int QtdResquisicao { get; set; }

    public string HtmlExemplo { get; set; }

    public ServidorHttp(int porta = 8080)
    {
        Porta = porta;
        this.CriarHtmlExemplo();
        try
        {
            Controlador = new TcpListener(IPAddress.Parse("127.0.0.1"), Porta);
            Controlador.Start();
            Console.WriteLine($"O servidor está rodando na porta {Porta}. ");
            Console.WriteLine($"Para acessar, digite no navegador http://localhost:{Porta}.");
            Task servidorHttpTask = Task.Run(() => AguardarRequisicao());
            servidorHttpTask.GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Erro ao iniciar o servidor na porta {Porta}: \n{e.Message}");
        }
    }

    private async Task AguardarRequisicao()
    {
        while (true)
        {
            Socket conexao = await Controlador.AcceptSocketAsync();
            QtdResquisicao++;

            Task task = Task.Run(() => ProcessarRequisicao(conexao, QtdResquisicao));
        }
    }

    private void ProcessarRequisicao(Socket conexao, int numeroRequisicao)
    {
        Console.WriteLine($"Processando requisição #{numeroRequisicao}...\n");

        if (conexao.Connected)
        {
            //Espaço de memória que vai armazenar os dados da requisição
            byte[] bytesRequisicao = new byte[1024];
            conexao.Receive(bytesRequisicao, bytesRequisicao.Length, 0);
            string textoRequisicao = Encoding.UTF8.GetString(bytesRequisicao).Replace((char)0, ' ').Trim();

            if (textoRequisicao.Length > 0)
            {
                //O texto exibido é a requisição HTTP que está sendo realizada
                Console.WriteLine($"\n{textoRequisicao}\n");

                string[] linhas = textoRequisicao.Split("\r\n");
                int iPrimeiroEspaco = linhas[0].IndexOf(' ');
                int iSegundoEspaco = linhas[0].LastIndexOf(' ');
                string metodoHttp = linhas[0].Substring(0, iPrimeiroEspaco);
                string recursoBuscado = linhas[0].Substring(iPrimeiroEspaco + 1,
                iSegundoEspaco - iPrimeiroEspaco - 1);
                string versaoHttp = linhas[0].Substring(iSegundoEspaco + 1);
                iPrimeiroEspaco = linhas[1].IndexOf(' ');
                string nomeHost = linhas[1].Substring(iPrimeiroEspaco + 1);

                byte[] bytesCabecalho = null;
                var bytesConteudo = LerArquivo(recursoBuscado);
                if (bytesConteudo.Length > 0)
                {
                    bytesCabecalho = GerarCabecalho(versaoHttp, "text/html;charset=utf-8", "200", bytesConteudo.Length);
                }
                else
                {
                    bytesConteudo = Encoding.UTF8.GetBytes("<h1>Erro 404 - Arquivo não encontrado!</h1>");
                    bytesCabecalho = GerarCabecalho(versaoHttp, "text/html;charset=utf-8", "404", bytesConteudo.Length);
                }

                int bytesEnviados = conexao.Send(bytesCabecalho, bytesCabecalho.Length, 0);
                bytesEnviados += conexao.Send(bytesConteudo, bytesConteudo.Length, 0);
                conexao.Close();
                Console.WriteLine($"\n{bytesEnviados} bytes enviados em resposta à requisição #{numeroRequisicao}.");
            }
        }

        Console.WriteLine($"\nRequisição {numeroRequisicao} finalizado.");
    }

    public byte[] GerarCabecalho(string versaoHttp, string tipoMime,
    string codigoHttp, int qtdBytes = 0)
    {
        StringBuilder texto = new StringBuilder();
        texto.Append($"{versaoHttp} {codigoHttp} {Environment.NewLine}");
        texto.Append($"Server: Servidor Http 1.0 {Environment.NewLine}");
        texto.Append($"Content-Type: {tipoMime} {Environment.NewLine}");
        texto.Append($"Content-Length: {qtdBytes} {Environment.NewLine}{Environment.NewLine}");
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

    public byte[] LerArquivo(string recurso)
    {
        string diretorio = "C:\\dev\\cursos\\ServidorHTTP\\www";
        string caminhoArquivo = diretorio + recurso.Replace("/", "\\");

        if (File.Exists(caminhoArquivo))
            return File.ReadAllBytes(caminhoArquivo);
        else
            return new byte[0];
    }


}