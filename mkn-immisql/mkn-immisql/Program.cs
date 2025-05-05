using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MknImmiSql.Api.V1;

namespace MknImmiSql;

public static class Program
{
    public static void Main(String[] argv)
    {
    //     Parser ps = new Parser(new Tokenizer());
    //     ISqlCommand com = ps.ApplyCommand("CREATE TABLE students (id SERIAL PRIMARY KEY, name STRING NOT NULL, last_name STRING NOT NULL)");
    //     com.Execute();
    //
    //     com = ps.ApplyCommand("INSERT INTO students (name, last_name) VALUES ('Alexandr', 'Alexandrov'), ('Evgeny', 'Evgeniev'), ('Ivan', 'Ivanov'), ('Kirill', 'Kirillov'), ('Pavel', 'Pavlov'), ('Yan', 'Yanov')");
    //     com.Execute();
    //
    //     com = ps.ApplyCommand("CREATE TABLE scores (student_id INTEGER NOT NULL, score INTEGER NOT NULL)");
    //     com.Execute();
    //
    //     com = ps.ApplyCommand("INSERT INTO scores (student_id, score) VALUES (1, 10), (2, 20), (3, 30), (7, 70)");
    //     com.Execute();
    //
    //     com = ps.ApplyCommand("SELECT students.name AS name, scores.score AS score FROM students RIGHT JOIN scores ON students.id = scores.student_id");
    //     com.Execute();

        if (true)
        {
            Console.WriteLine(ServiceContext.Instance.Token);

            try
            {
                WebApplicationBuilder builder = WebApplication.CreateBuilder(argv);

                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen();
                builder.Services.AddControllers();

                using (WebApplication app = builder.Build())
                {
                    if (app.Environment.IsDevelopment())
                    {
                        app.UseSwagger();
                        app.UseSwaggerUI();
                    }

                    app.MapControllers();
                    app.Run();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Fatal error: {e.Message}");
                Console.WriteLine(e.StackTrace);
            }
        }
    }
}
