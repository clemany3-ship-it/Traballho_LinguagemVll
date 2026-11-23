using System.Globalization;
using System.Text;

namespace FaturacaoLoja;

// ---------------------------------------------------------------
//  Sistema de Faturação - Loja de Produtos Alimentares (Console)
//  Tudo num só ficheiro, sem camadas nem arquitetura.
// ---------------------------------------------------------------

class Cliente
{
    public string Nome { get; set; } = "";
    public char Genero { get; set; }          // 'M' ou 'F'
    public int Idade { get; set; }
    public bool Gravida { get; set; }
    public bool Deficiente { get; set; }

    public bool Idoso => Idade >= Program.IdadeIdoso;
    public bool Prioritario => Gravida || Idoso || Deficiente;

    // Motivo(s) da prioridade no atendimento (nada a ver com o bónus comercial)
    public string MotivoPrioridade()
    {
        var motivos = new List<string>();
        if (Gravida) motivos.Add("Grávida");
        if (Idoso) motivos.Add("Idoso");
        if (Deficiente) motivos.Add("Pessoa com deficiência");
        return motivos.Count > 0 ? string.Join(", ", motivos) : "Normal";
    }
}

class Fatura
{
    public int Numero { get; set; }
    public DateTime Data { get; set; }
    public Cliente Cliente { get; set; } = new Cliente();
    public decimal ValorCompras { get; set; }
    public bool TemBonus { get; set; }
    public decimal ValorBonus { get; set; }
    public decimal ValorPagar => ValorCompras - ValorBonus;
}

class Program
{
    // Regras de negócio
    public const decimal LimiteBonus = 100_000m;   // bónus só se o total for SUPERIOR a este valor
    public const decimal TaxaBonus = 0.10m;        // 10%
    public const int IdadeIdoso = 60;

    static readonly CultureInfo Cultura = ObterCultura();

    static readonly List<Fatura> faturas = new();
    static readonly Queue<Fatura> filaPrioritaria = new();  // grávidas, idosos, deficientes
    static readonly Queue<Fatura> filaNormal = new();       // restantes clientes
    static int proximoNumero = 1;

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        int opcao;
        do
        {
            MostrarMenu();
            opcao = LerInteiro("Opção: ", 0, 4);
            Console.WriteLine();

            switch (opcao)
            {
                case 1: RegistarCompra(); break;
                case 2: MostrarFila(); break;
                case 3: AtenderProximo(); break;
                case 4: ListarFaturas(); break;
                case 0: Console.WriteLine("Até logo! Obrigado por usar o sistema."); break;
            }

            if (opcao != 0)
            {
                Console.WriteLine("\nPrima ENTER para continuar...");
                Console.ReadLine();
            }
        } while (opcao != 0);
    }

    // ------------------------- MENU -------------------------

    static void MostrarMenu()
    {
        Console.Clear();
        Console.WriteLine("==========================================");
        Console.WriteLine("   LOJA DE PRODUTOS ALIMENTARES - FATURAÇÃO");
        Console.WriteLine("==========================================");
        Console.WriteLine(" 1 - Registar cliente e compra");
        Console.WriteLine(" 2 - Ver fila de atendimento");
        Console.WriteLine(" 3 - Atender próximo cliente");
        Console.WriteLine(" 4 - Listar faturas emitidas");
        Console.WriteLine(" 0 - Sair");
        Console.WriteLine("------------------------------------------");
        Console.WriteLine($" Em espera: {filaPrioritaria.Count} prioritário(s) | {filaNormal.Count} normal(is)");
        Console.WriteLine("==========================================");
    }

    // ------------------- 1. REGISTAR COMPRA -------------------

    static void RegistarCompra()
    {
        Console.WriteLine("--- REGISTAR CLIENTE E COMPRA ---\n");

        var cliente = new Cliente
        {
            Nome = LerTexto("Nome do cliente: "),
            Genero = LerGenero(),
            Idade = LerInteiro("Idade: ", 0, 120)
        };

        // A pergunta sobre gravidez só faz sentido para o género feminino
        if (cliente.Genero == 'F')
            cliente.Gravida = LerSimNao("Está grávida? (S/N): ");

        cliente.Deficiente = LerSimNao("Tem alguma deficiência? (S/N): ");

        decimal valor = LerValor("Valor total das compras (Kz): ");

        // Bónus comercial: depende APENAS do valor da compra
        bool temBonus = valor > LimiteBonus;
        decimal valorBonus = temBonus ? valor * TaxaBonus : 0m;

        var fatura = new Fatura
        {
            Numero = proximoNumero++,
            Data = DateTime.Now,
            Cliente = cliente,
            ValorCompras = valor,
            TemBonus = temBonus,
            ValorBonus = valorBonus
        };

        faturas.Add(fatura);

        // Prioridade no atendimento: depende APENAS da condição do cliente
        if (cliente.Prioritario)
            filaPrioritaria.Enqueue(fatura);
        else
            filaNormal.Enqueue(fatura);

        Console.WriteLine();
        ImprimirFatura(fatura);
        Console.WriteLine(cliente.Prioritario
            ? "\n>> Cliente adicionado à fila PRIORITÁRIA."
            : "\n>> Cliente adicionado à fila NORMAL.");
    }

    // ------------------- 2. VER FILA -------------------

    static void MostrarFila()
    {
        Console.WriteLine("--- FILA DE ATENDIMENTO ---\n");

        if (filaPrioritaria.Count == 0 && filaNormal.Count == 0)
        {
            Console.WriteLine("Não há clientes em espera.");
            return;
        }

        int posicao = 1;

        Console.WriteLine("[ PRIORITÁRIOS ]");
        if (filaPrioritaria.Count == 0) Console.WriteLine("  (vazio)");
        foreach (var f in filaPrioritaria)
            Console.WriteLine($"  {posicao++,2}. {f.Cliente.Nome,-25} {f.Cliente.MotivoPrioridade()}");

        Console.WriteLine("\n[ NORMAIS ]");
        if (filaNormal.Count == 0) Console.WriteLine("  (vazio)");
        foreach (var f in filaNormal)
            Console.WriteLine($"  {posicao++,2}. {f.Cliente.Nome,-25} Normal");
    }

    // ------------------- 3. ATENDER -------------------

    static void AtenderProximo()
    {
        Console.WriteLine("--- ATENDER PRÓXIMO CLIENTE ---\n");

        Fatura? atual = null;

        // Os prioritários passam sempre à frente; dentro de cada fila, ordem de chegada
        if (filaPrioritaria.Count > 0) atual = filaPrioritaria.Dequeue();
        else if (filaNormal.Count > 0) atual = filaNormal.Dequeue();

        if (atual == null)
        {
            Console.WriteLine("Não há clientes em espera.");
            return;
        }

        Console.WriteLine($"A atender: {atual.Cliente.Nome}  [{atual.Cliente.MotivoPrioridade()}]\n");
        ImprimirFatura(atual);
    }

    // ------------------- 4. LISTAR FATURAS -------------------

    static void ListarFaturas()
    {
        Console.WriteLine("--- FATURAS EMITIDAS ---\n");

        if (faturas.Count == 0)
        {
            Console.WriteLine("Ainda não foram emitidas faturas.");
            return;
        }

        Console.WriteLine($"{"Nº",-4} {"Cliente",-22} {"Compras",16} {"Bónus",14} {"A pagar",16}");
        Console.WriteLine(new string('-', 76));

        foreach (var f in faturas)
        {
            Console.WriteLine($"{f.Numero,-4} {Cortar(f.Cliente.Nome, 22),-22} " +
                              $"{Kz(f.ValorCompras),16} {Kz(f.ValorBonus),14} {Kz(f.ValorPagar),16}");
        }

        Console.WriteLine(new string('-', 76));
        Console.WriteLine($"Total faturado: {Kz(faturas.Sum(f => f.ValorPagar))}");
    }

    // ------------------- FATURA -------------------

    static void ImprimirFatura(Fatura f)
    {
        Console.WriteLine("==========================================");
        Console.WriteLine($" FATURA Nº {f.Numero:D4}        {f.Data:dd/MM/yyyy HH:mm}");
        Console.WriteLine("==========================================");
        Console.WriteLine($" Cliente    : {f.Cliente.Nome}");
        Console.WriteLine($" Género     : {(f.Cliente.Genero == 'F' ? "Feminino" : "Masculino")}");
        Console.WriteLine($" Idade      : {f.Cliente.Idade} anos");
        Console.WriteLine($" Atendimento: {f.Cliente.MotivoPrioridade()}");
        Console.WriteLine("------------------------------------------");
        Console.WriteLine($" Total compras : {Kz(f.ValorCompras),20}");
        if (f.TemBonus)
            Console.WriteLine($" Bónus (10%)   : {"-" + Kz(f.ValorBonus),20}");
        else
            Console.WriteLine($" Bónus         : {"Sem direito",20}");
        Console.WriteLine("------------------------------------------");
        Console.WriteLine($" TOTAL A PAGAR : {Kz(f.ValorPagar),20}");
        Console.WriteLine("==========================================");
    }

    // ------------------- LEITURA DE DADOS -------------------

    static string LerTexto(string mensagem)
    {
        while (true)
        {
            Console.Write(mensagem);
            string? texto = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(texto)) return texto;
            Console.WriteLine("  Valor inválido. Não pode ficar vazio.");
        }
    }

    static char LerGenero()
    {
        while (true)
        {
            Console.Write("Género (M/F): ");
            string? texto = Console.ReadLine()?.Trim().ToUpper();
            if (texto == "M" || texto == "F") return texto[0];
            Console.WriteLine("  Digite M para masculino ou F para feminino.");
        }
    }

    static bool LerSimNao(string mensagem)
    {
        while (true)
        {
            Console.Write(mensagem);
            string? texto = Console.ReadLine()?.Trim().ToUpper();
            if (texto == "S") return true;
            if (texto == "N") return false;
            Console.WriteLine("  Responda com S ou N.");
        }
    }

    static int LerInteiro(string mensagem, int min, int max)
    {
        while (true)
        {
            Console.Write(mensagem);
            if (int.TryParse(Console.ReadLine(), out int n) && n >= min && n <= max)
                return n;
            Console.WriteLine($"  Digite um número inteiro entre {min} e {max}.");
        }
    }

    static decimal LerValor(string mensagem)
    {
        while (true)
        {
            Console.Write(mensagem);
            string texto = (Console.ReadLine() ?? "").Trim().Replace(',', '.');

            // Aceita tanto "150000,50" como "150000.50"
            if (decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal v) && v > 0)
                return Math.Round(v, 2);

            Console.WriteLine("  Valor inválido. Digite um número maior que zero.");
        }
    }

    // ------------------- UTILITÁRIOS -------------------

    static string Kz(decimal valor) => valor.ToString("N2", Cultura) + " Kz";

    static string Cortar(string texto, int max) =>
        texto.Length <= max ? texto : texto.Substring(0, max - 1) + ".";

    static CultureInfo ObterCultura()
    {
        try { return new CultureInfo("pt-PT"); }
        catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
    }
}
