using System.Text.Json.Serialization;
using Fetchle.Evals.Queries;

namespace Fetchle.Evals.Reporting;

[JsonSerializable(typeof(EvalQuery[]))]
[JsonSerializable(typeof(EvalReport))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, WriteIndented = true)]
partial class EvalJson : JsonSerializerContext;
