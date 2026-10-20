using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDataContext>();
var app = builder.Build();

app.MapGet("/", () => "API de Folha de Pagamento Ativa");

app.MapPost("/api/folha/cadastrar", ([FromBody] Folha folha, [FromServices] AppDataContext ctx) =>
{
    // Validações obrigatórias
    if (folha.Mes < 1 || folha.Mes > 12)
    {
        return Results.BadRequest(new { mensagem = "O mês deve estar entre 1 e 12." });
    }
    if (folha.Ano < 2000)
    {
        return Results.BadRequest(new { mensagem = "O ano deve ser maior ou igual a 2000." });
    }

    // Validação: Não permitir duas folhas com o mesmo CPF, mês e ano
    bool folhaExiste = ctx.Folhas.Any(x => x.Cpf == folha.Cpf && x.Mes == folha.Mes && x.Ano == folha.Ano);
    if (folhaExiste)
    {
        return Results.BadRequest(new { mensagem = "Já existe uma folha cadastrada para este funcionário neste mês e ano." });
    }
    
    // Salário Bruto
    folha.SalarioBruto = folha.HorasTrabalhadas * folha.ValorHora;

    // Imposto de Renda (IR)
    double ir = 0;
    if (folha.SalarioBruto >= 1903.99 && folha.SalarioBruto <= 2826.65)
    {
        ir = (folha.SalarioBruto * 0.075) - 142.80;
    }
    else if (folha.SalarioBruto >= 2826.66 && folha.SalarioBruto <= 3751.05)
    {
        ir = (folha.SalarioBruto * 0.15) - 354.80;
    }
    else if (folha.SalarioBruto >= 3751.06 && folha.SalarioBruto <= 4664.68)
    {
        ir = (folha.SalarioBruto * 0.225) - 636.13;
    }
    else if (folha.SalarioBruto > 4664.68)
    {
        ir = (folha.SalarioBruto * 0.275) - 869.36;
    }
    folha.ImpostoRenda = Math.Round(ir > 0 ? ir : 0, 2);

    // INSS
    double inss = 0;
    if (folha.SalarioBruto <= 1693.72)
    {
        inss = folha.SalarioBruto * 0.08;
    }
    else if (folha.SalarioBruto >= 1693.73 && folha.SalarioBruto <= 2822.90)
    {
        inss = folha.SalarioBruto * 0.09;
    }
    else if (folha.SalarioBruto >= 2822.91 && folha.SalarioBruto <= 5645.80)
    {
        inss = folha.SalarioBruto * 0.11;
    }
    else
    {
        inss = 621.03; // Fixo para cima de 5.645,81
    }
    folha.Inss = Math.Round(inss, 2);

    // FGTS (não desconta do salário líquido)
    folha.Fgts = Math.Round(folha.SalarioBruto * 0.08, 2);

    // Salário Líquido
    folha.SalarioLiquido = Math.Round(folha.SalarioBruto - folha.ImpostoRenda - folha.Inss, 2);

    // Salvar no Banco
    ctx.Folhas.Add(folha);
    ctx.SaveChanges();

    return Results.Created($"/api/folha/buscar/{folha.Cpf}/{folha.Mes}/{folha.Ano}", folha);
});

app.MapGet("/api/folha/listar", ([FromServices] AppDataContext ctx) =>
{
    if (!ctx.Folhas.Any())
    {
        return Results.NotFound(new { mensagem = "Nenhum registro encontrado." });
    }

    return Results.Ok(ctx.Folhas.ToList());
});

app.MapGet("/api/folha/buscar/{cpf}/{mes}/{ano}", ([FromRoute] string cpf, [FromRoute] int mes, [FromRoute] int ano, [FromServices] AppDataContext ctx) =>
{
    var folha = ctx.Folhas.FirstOrDefault(x => x.Cpf == cpf && x.Mes == mes && x.Ano == ano);

    if (folha is null)
    {
        return Results.NotFound(new { mensagem = "Folha de pagamento não encontrada." });
    }

    return Results.Ok(folha);
});

app.MapDelete("/api/folha/remover/{cpf}/{mes}/{ano}", ([FromRoute] string cpf, [FromRoute] int mes, [FromRoute] int ano, [FromServices] AppDataContext ctx) =>
{
    var folha = ctx.Folhas.FirstOrDefault(x => x.Cpf == cpf && x.Mes == mes && x.Ano == ano);

    if (folha is null)
    {
        return Results.NotFound(new { mensagem = "Folha de pagamento não encontrada para remoção." });
    }

    ctx.Folhas.Remove(folha);
    ctx.SaveChanges();
    
    return Results.Ok(folha);
});

app.MapGet("/api/folha/total-liquido", ([FromServices] AppDataContext ctx) =>
{
    if (!ctx.Folhas.Any())
    {
        return Results.NotFound(new { mensagem = "Nenhum registro encontrado para cálculo." });
    }

    double totalLiquido = ctx.Folhas.Sum(x => x.SalarioLiquido);

    return Results.Ok(new { totalLiquido = Math.Round(totalLiquido, 2) });
});

app.Run();
