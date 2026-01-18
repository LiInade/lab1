using System.Diagnostics;
using System.Text.Json;
using ReflectionCsvBenchmark;

var iterations = 100_000;

var f = new F();

WarmUp(f);

var csv = "";
var sw = Stopwatch.StartNew();
for (int i = 0; i < iterations; i++)
{
    csv = CsvReflectionSerializer.Serialize(f);
}
sw.Stop();
var csvSerializeMs = sw.ElapsedMilliseconds;

Console.WriteLine("CSV result:");
sw.Restart();
Console.WriteLine(csv);
sw.Stop();
var consoleWriteMs = sw.ElapsedMilliseconds;

F? fromCsv = null;
sw.Restart();
for (int i = 0; i < iterations; i++)
{
    fromCsv = CsvReflectionSerializer.Deserialize<F>(csv);
}
sw.Stop();
var csvDeserializeMs = sw.ElapsedMilliseconds;

var json = "";
sw.Restart();
for (int i = 0; i < iterations; i++)
{
    json = JsonSerializer.Serialize(f);
}
sw.Stop();
var jsonSerializeMs = sw.ElapsedMilliseconds;

F? fromJson = null;
sw.Restart();
for (int i = 0; i < iterations; i++)
{
    fromJson = JsonSerializer.Deserialize<F>(json);
}
sw.Stop();
var jsonDeserializeMs = sw.ElapsedMilliseconds;

Console.WriteLine();
Console.WriteLine($"Количество итераций = {iterations}");
Console.WriteLine($"CSV сериализация: {csvSerializeMs} мс");
Console.WriteLine($"Вывод в консоль: {consoleWriteMs} мс");
Console.WriteLine($"CSV deserialize: {csvDeserializeMs} мс");
Console.WriteLine($"JSON сериализация (System.Text.Json): {jsonSerializeMs} мс");
Console.WriteLine($"JSON десериализация (System.Text.Json): {jsonDeserializeMs} мс");

static void WarmUp(F f)
{
    for (int i = 0; i < 5000; i++)
    {
        var csv = CsvReflectionSerializer.Serialize(f);
        var _ = CsvReflectionSerializer.Deserialize<F>(csv);
        var json = JsonSerializer.Serialize(f);
        var __ = JsonSerializer.Deserialize<F>(json);
    }

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
}
