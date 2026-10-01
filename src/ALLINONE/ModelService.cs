using LLama;
using LLama.Common;
using LLama.Sampling;
using LLama.Transformers;

namespace ALLINONE;

public sealed class ModelService : IDisposable
{
    private readonly string modelPath;
    private readonly SemaphoreSlim generationLock = new(1, 1);
    private LLamaWeights? model;
    private ChatSession? session;

    public string ModelId => "ALLINONE Local AI";
    public bool IsConfigured => File.Exists(modelPath);
    public string ModelPath => modelPath;

    public ModelService()
    {
        modelPath = Path.Combine(AppContext.BaseDirectory, "Models", "allinone.gguf");
    }

    public async Task<string> GenerateAsync(
        string prompt,
        string? context = null,
        string? role = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return $"ALLINONE's local AI model is not installed yet. Place the model weights at:{Environment.NewLine}{modelPath}";
        }

        await generationLock.WaitAsync(cancellationToken);
        try
        {
            EnsureLoaded();

            var system = role switch
            {
                "code" => "You are CodeInOne, the coding intelligence inside ALLINONE. Write correct, practical code and be honest about what was actually changed.",
                "game" => "You are ALLINONE's game-building intelligence. Help design games, gameplay systems, code, project structure, and safe creative features.",
                "research" => "You are SearchInOne's research intelligence. Use the supplied web sources as evidence, distinguish facts from reasoning, and do not invent sources.",
                _ => "You are ALLINONE, a helpful local AI assistant. Be accurate, concise, transparent, and useful."
            };

            var fullPrompt = string.IsNullOrWhiteSpace(context)
                ? prompt
                : $"{prompt}\n\nSearchInOne source context:\n{context}";

            session!.ChatHistory.AddMessage(AuthorRole.System, system);

            var inference = new InferenceParams
            {
                MaxTokens = 512,
                SamplingPipeline = new DefaultSamplingPipeline
                {
                    Temperature = 0.6f
                }
            };

            var result = new System.Text.StringBuilder();
            await foreach (var token in session.ChatAsync(
                new ChatHistory.Message(AuthorRole.User, fullPrompt),
                inference,
                cancellationToken))
            {
                result.Append(token);
            }

            var answer = result.ToString().Trim();
            return string.IsNullOrWhiteSpace(answer)
                ? "The local model returned no text."
                : answer;
        }
        finally
        {
            generationLock.Release();
        }
    }

    private void EnsureLoaded()
    {
        if (session is not null) return;

        var parameters = new ModelParams(modelPath)
        {
            ContextSize = 2048,
            GpuLayerCount = 0
        };

        model = LLamaWeights.LoadFromFile(parameters);
        var context = model.CreateContext(parameters);
        var executor = new InteractiveExecutor(context);

        session = new ChatSession(executor);
        session.WithHistoryTransform(new PromptTemplateTransformer(model, withAssistant: true));
    }

    public void Dispose()
    {
        generationLock.Dispose();
        model?.Dispose();
    }
}
