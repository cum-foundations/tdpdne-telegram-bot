namespace TDPDNE.Telegram.Bot.Services;

using Abstract;
using Serilog;
using TDPDNE.Telegram.Bot.Configs;

public class TDPDNEWrapper : ITDPDNEWrapper
{
    private readonly WrapperConfiguration _configuration;
    private readonly ILogger<TDPDNEWrapper> _logger;
    private readonly Random _random;

    public TDPDNEWrapper(IConfiguration configuration)
    {
        _random = new Random();
        _logger = LoggerFactory
            .Create(builder => builder.AddSerilog())
            .CreateLogger<TDPDNEWrapper>();
        _configuration = configuration
            .GetRequiredSection(WrapperConfiguration.Configuration)
            .Get<WrapperConfiguration>() ?? throw new ArgumentNullException(WrapperConfiguration.Configuration);
    }

    public Stream GetPicture(CancellationToken cancellationToken)
    {
        // TODO: move folder name 2 config
        var folderName = "content";
        string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folderName);

        string[] imageFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly);
        string[] extensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };


        string? imagePath = null;
        foreach (var file in imageFiles)
        {
            if (Array.Exists(extensions, ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            {
                imagePath = file;
                break;
            }
        }

        if (imagePath == null)
        {
            _logger.LogError($"❌ There are no images in the {folderName} folder.");
            return Stream.Null;
        }

        using var imageStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read);
        _logger.LogInformation($"✅ Загружено изображение: {Path.GetFileName(imagePath)}");
        _logger.LogInformation($"Размер: {imageStream.Length} байт");

        using var memoryStream = new MemoryStream();
        imageStream.CopyTo(memoryStream);

        return memoryStream;
    }
}
