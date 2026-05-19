using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Kronk.Models.Tools;

public class FetchUrlProperties
{
    [JsonPropertyName("url")]
    public StringToolProperty Url { get; set; } = new("The full HTTP/HTTPS URL to read");
}

public class FetchUrlToolInputSchema : ToolParameters<FetchUrlProperties>
{
    public FetchUrlToolInputSchema() : base([nameof(FetchUrlProperties.Url)])
    {
    }
}

public class FetchUrlToolArguments
{
    public required string Url { get; set; }
}

public class FetchUrlTool : Tool<FetchUrlToolArguments, FetchUrlToolInputSchema>
{
    private const int MaxLength = 15000;
    public static HttpClient HttpClient { get; } = new HttpClient();

    public FetchUrlTool() : base("fetch_url", "Fetches the readable text content of a specific webpage URL.")
    {
    }

    public override async Task<string> Run(FetchUrlToolArguments arguments, CancellationToken cancellationToken)
    {
        var url = arguments.Url;

        // Add User-Agent if missing
        if (!HttpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            HttpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) KronkAgent/1.0");
        }

        // 1. Fetch the raw HTML directly
        string html = await HttpClient.GetStringAsync(url, cancellationToken);

        if (string.IsNullOrEmpty(html)) return "No content found.";

        // 2. Parse with HtmlAgilityPack
        var doc = new HtmlDocument();
        doc.OptionFixNestedTags = true;
        doc.LoadHtml(html);

        // 3. Select content
        var contentNode = doc.DocumentNode.SelectSingleNode("//article | //main")
                         ?? doc.DocumentNode.SelectSingleNode("//body")
                         ?? doc.DocumentNode;

        // 4. Convert to Markdown
        var markdown = new StringBuilder();
        ProcessNode(contentNode, markdown, new Uri(url), indentLevel: 0);

        var toolResult = markdown.ToString().Trim();

        // 5. Sanitize: Remove invalid XML/JSON characters (Control chars < 0x20 except tab/newline)
        // This prevents "Failed to Tokenize input" errors caused by hidden garbage in HTML
        toolResult = Regex.Replace(toolResult, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");

        // 6. Truncate if necessary
        if (toolResult.Length > MaxLength)
        {
            toolResult = toolResult[..MaxLength] + "\n\n[Content truncated for length]";
        }

        return toolResult;
    }

    private static void ProcessNode(HtmlNode node, StringBuilder sb, Uri baseUrl, int indentLevel)
    {
        // Handle Text Nodes
        if (node.NodeType == HtmlNodeType.Text)
        {
            string text = node.InnerText.Trim();
            // Collapse whitespace
            text = Regex.Replace(text, @"\s+", " ");
            if (!string.IsNullOrEmpty(text)) sb.Append(text);
            return;
        }

        // Skip invisible scripts
        if (node.Name == "script" || node.Name == "style") return;

        switch (node.Name)
        {
            case "h1": AppendHeader(sb, node, 1); break;
            case "h2": AppendHeader(sb, node, 2); break;
            case "h3": AppendHeader(sb, node, 3); break;
            case "h4": AppendHeader(sb, node, 4); break;
            case "h5": AppendHeader(sb, node, 5); break;
            case "h6": AppendHeader(sb, node, 6); break;
            case "p": AppendBlock(sb, node, indentLevel); break;

            // Lists
            case "ul":
            case "ol":
                foreach (var child in node.ChildNodes) ProcessNode(child, sb, baseUrl, indentLevel);
                break;

            case "li":
                sb.Append(new string(' ', indentLevel * 2));
                sb.Append("* ");
                foreach (var child in node.ChildNodes) ProcessNode(child, sb, baseUrl, indentLevel + 1);
                sb.AppendLine();
                break;

            case "a": AppendLink(sb, node, baseUrl); break;
            case "img": AppendImage(sb, node, baseUrl); break;

            // Formatting
            case "strong":
            case "b": AppendFormatting(sb, node, "**"); break;
            case "em":
            case "i": AppendFormatting(sb, node, "*"); break;

            // Code
            case "code":
                if (node.ParentNode?.Name != "pre")
                {
                    sb.Append("`");
                    sb.Append(node.InnerText);
                    sb.Append("`");
                }
                break;

            case "pre":
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine(node.InnerText);
                sb.AppendLine("```");
                sb.AppendLine();
                break;

            case "blockquote":
                sb.AppendLine();
                sb.AppendLine("> ");
                foreach (var child in node.ChildNodes) ProcessNode(child, sb, baseUrl, indentLevel);
                sb.AppendLine();
                break;

            case "hr":
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
                break;

            case "br":
                sb.AppendLine();
                break;

            default:
                foreach (var child in node.ChildNodes) ProcessNode(child, sb, baseUrl, indentLevel);
                break;
        }
    }

    private static void AppendHeader(StringBuilder sb, HtmlNode node, int level)
    {
        sb.AppendLine();
        sb.Append(new string('#', level));
        sb.Append(" ");
        sb.AppendLine(node.InnerText.Trim());
        sb.AppendLine();
    }

    private static void AppendBlock(StringBuilder sb, HtmlNode node, int indentLevel)
    {
        sb.AppendLine();
        sb.Append(new string(' ', indentLevel * 2));
        sb.AppendLine(node.InnerText.Trim());
        sb.AppendLine();
    }

    private static void AppendLink(StringBuilder sb, HtmlNode node, Uri baseUrl)
    {
        string text = node.InnerText.Trim();
        string href = node.GetAttributeValue("href", "#");
        string absoluteUrl = ResolveUrl(baseUrl, href);

        sb.Append("[");
        sb.Append(text);
        sb.Append("](");
        sb.Append(absoluteUrl);
        sb.Append(")");
    }

    private static void AppendImage(StringBuilder sb, HtmlNode node, Uri baseUrl)
    {
        string alt = node.GetAttributeValue("alt", "Image");
        string src = node.GetAttributeValue("src", "");
        string absoluteUrl = ResolveUrl(baseUrl, src);

        sb.Append("![");
        sb.Append(alt);
        sb.Append("](");
        sb.Append(absoluteUrl);
        sb.Append(")");
    }

    private static void AppendFormatting(StringBuilder sb, HtmlNode node, string tag)
    {
        sb.Append(tag);
        sb.Append(node.InnerText);
        sb.Append(tag);
    }

    private static string ResolveUrl(Uri baseUrl, string relativeUrl)
    {
        if (string.IsNullOrEmpty(relativeUrl)) return "";
        try
        {
            return new Uri(baseUrl, relativeUrl).ToString();
        }
        catch
        {
            return relativeUrl;
        }
    }
}
