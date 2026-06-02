using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using UltraLightApiTester.Models;

namespace UltraLightApiTester.Services
{
    public class SwaggerParseResult
    {
        public List<SwaggerEndpoint> Endpoints = new List<SwaggerEndpoint>();
        public string Title = "";
    }

    public static class SwaggerParser
    {
        private static readonly JavaScriptSerializer Js = new JavaScriptSerializer();

        public static async Task<SwaggerParseResult> ParseFromUrl(string url)
        {
            var response = await HttpSingleton.Instance.GetStringAsync(url);
            return Parse(response, url);
        }

        public static SwaggerParseResult ParseFromFile(string filePath)
        {
            var json = File.ReadAllText(filePath);
            return Parse(json, filePath);
        }

        private static SwaggerParseResult Parse(string json, string sourceUrl)
        {
            var result = new SwaggerParseResult();
            var endpoints = result.Endpoints;
            var root = Js.Deserialize<Dictionary<string, object>>(json);

            var isV3 = root.ContainsKey("openapi");

            // extract info.title
            if (root.TryGetValue("info", out var infoObj) && infoObj is Dictionary<string, object> info)
                result.Title = (info.TryGetValue("title", out var t) ? (t as string ?? "") : "").Trim();

            // collect all named schemas for $ref resolution
            var allSchemas = new Dictionary<string, object>();
            if (isV3 && root.TryGetValue("components", out var comp) && comp is Dictionary<string, object> compDict)
            {
                if (compDict.TryGetValue("schemas", out var schemas) && schemas is Dictionary<string, object> sd)
                    foreach (var kv in sd) allSchemas[kv.Key] = kv.Value;
            }
            else if (!isV3 && root.TryGetValue("definitions", out var defs) && defs is Dictionary<string, object> defDict)
            {
                foreach (var kv in defDict) allSchemas[kv.Key] = kv.Value;
            }

            if (!root.TryGetValue("paths", out var pathsObj) || !(pathsObj is Dictionary<string, object> paths))
                return result;

            var httpMethods = new HashSet<string> { "get", "post", "put", "delete", "patch", "options", "head" };

            foreach (var pathEntry in paths)
            {
                if (!(pathEntry.Value is Dictionary<string, object> pathOps)) continue;

                foreach (var opEntry in pathOps)
                {
                    if (!httpMethods.Contains(opEntry.Key.ToLower())) continue;
                    if (!(opEntry.Value is Dictionary<string, object> op)) continue;

                    var path = pathEntry.Key;
                    var fullUrl = "{{baseUrl}}" + path;

                    var ep = new SwaggerEndpoint
                    {
                        Path = fullUrl,
                        Method = opEntry.Key.ToUpper(),
                        Summary = op.TryGetValue("summary", out var summ) ? (summ as string ?? "") : ""
                    };

                    // parameters
                    if (op.TryGetValue("parameters", out var paramList) && paramList is ArrayList parr)
                    {
                        foreach (var pobj in parr)
                        {
                            if (!(pobj is Dictionary<string, object> pdict)) continue;
                            var param = new SwaggerParameter();
                            param.Name = pdict.TryGetValue("name", out var pn) ? (pn as string ?? "") : "";
                            param.In = pdict.TryGetValue("in", out var pi) ? (pi as string ?? "") : "";
                            param.Required = pdict.TryGetValue("required", out var pr) && (pr as bool? ?? false);

                            if (pdict.TryGetValue("type", out var pt))
                                param.Type = pt as string ?? "";
                            else if (pdict.TryGetValue("schema", out var ps) && ps is Dictionary<string, object> sdict)
                                param.Type = (sdict.TryGetValue("type", out var st) ? st as string : "") ?? "";

                            ep.Parameters.Add(param);

                            // Swagger 2.0: body parameter
                            if (!isV3 && (pi as string) == "body" && pdict.TryGetValue("schema", out var bs) && bs is Dictionary<string, object> bsd)
                                ep.RequestBodyExample = GenerateExample(bsd, 0, allSchemas);
                        }
                    }

                    // requestBody (OpenAPI 3.x)
                    if (op.TryGetValue("requestBody", out var rb) && rb is Dictionary<string, object> rbdict)
                    {
                        if (rbdict.TryGetValue("content", out var ct) && ct is Dictionary<string, object> contentDict)
                        {
                            foreach (var ctEntry in contentDict)
                            {
                                ep.RequestBodyMediaType = ctEntry.Key;
                                if (ctEntry.Value is Dictionary<string, object> ctVal)
                                {
                                    if (ctVal.TryGetValue("example", out var example))
                                        ep.RequestBodyExample = Js.Serialize(example);
                                    else if (ctVal.TryGetValue("examples", out var examples) && examples is Dictionary<string, object> exDict)
                                    {
                                        foreach (var exEntry in exDict)
                                        {
                                            if (exEntry.Value is Dictionary<string, object> exVal && exVal.TryGetValue("value", out var v))
                                            { ep.RequestBodyExample = Js.Serialize(v); break; }
                                        }
                                    }
                                    else if (ctVal.TryGetValue("schema", out var schema) && schema is Dictionary<string, object> sdict)
                                        ep.RequestBodyExample = GenerateExample(sdict, 0, allSchemas);
                                }
                                break;
                            }
                        }
                    }

                    if (!isV3 && op.TryGetValue("consumes", out var consumes) && consumes is ArrayList clist && clist.Count > 0)
                        ep.RequestBodyMediaType = clist[0] as string ?? "";

                    endpoints.Add(ep);
                }
            }

            return result;
        }

        private static string GenerateExample(Dictionary<string, object> schema, int depth, Dictionary<string, object> schemas)
        {
            if (depth > 6) return "\"...\"";

            // resolve $ref
            if (schema.TryGetValue("$ref", out var refObj) && refObj is string refPath)
            {
                var parts = refPath.Split('/');
                var refName = parts[parts.Length - 1];
                if (schemas.TryGetValue(refName, out var resolved) && resolved is Dictionary<string, object> rsd)
                    return GenerateExample(rsd, depth + 1, schemas);
                return "\"...\"";
            }

            // direct example
            if (schema.TryGetValue("example", out var ex))
                return Js.Serialize(ex);

            // allOf composition
            if (schema.TryGetValue("allOf", out var allOf) && allOf is ArrayList allList)
            {
                var merged = new Dictionary<string, object>();
                foreach (var part in allList)
                {
                    if (part is Dictionary<string, object> pd)
                        foreach (var kv in pd) merged[kv.Key] = kv.Value;
                }
                if (!merged.ContainsKey("type")) merged["type"] = "object";
                return GenerateExample(merged, depth + 1, schemas);
            }

            // oneOf / anyOf - take first option
            ArrayList firstList = null;
            if (schema.TryGetValue("oneOf", out var oneOf) && oneOf is ArrayList ol1)
                firstList = ol1;
            else if (schema.TryGetValue("anyOf", out var anyOf) && anyOf is ArrayList ol2)
                firstList = ol2;
            if (firstList != null && firstList.Count > 0 && firstList[0] is Dictionary<string, object> fd)
                return GenerateExample(fd, depth + 1, schemas);

            // enum
            if (schema.TryGetValue("enum", out var enm) && enm is ArrayList enmList && enmList.Count > 0)
                return Js.Serialize(enmList[0]);

            // additionalProperties (map/dict)
            if (schema.TryGetValue("additionalProperties", out var addProp) && addProp is Dictionary<string, object> addSchema)
            {
                return "{\"key\": " + GenerateExample(addSchema, depth + 1, schemas) + "}";
            }

            if (!schema.TryGetValue("type", out var typeObj))
            {
                // no type but has properties - treat as object
                if (schema.TryGetValue("properties", out var pcheck) && pcheck is Dictionary<string, object>)
                    return GenerateObject(schema, depth, schemas);
                return "{}";
            }

            var type = typeObj as string ?? "object";

            switch (type)
            {
                case "object":
                    return GenerateObject(schema, depth, schemas);

                case "array":
                    if (schema.TryGetValue("items", out var items) && items is Dictionary<string, object> itemsDict)
                        return "[" + GenerateExample(itemsDict, depth + 1, schemas) + "]";
                    return "[\"...\"]";

                case "string":
                    if (schema.TryGetValue("format", out var fmt))
                    {
                        var f = fmt as string ?? "";
                        if (f == "date") return "\"2025-01-01\"";
                        if (f == "date-time") return "\"2025-01-01T00:00:00Z\"";
                        if (f == "email") return "\"user@example.com\"";
                        if (f == "uri" || f == "url") return "\"https://example.com\"";
                        if (f == "uuid") return "\"550e8400-e29b-41d4-a716-446655440000\"";
                    }
                    return "\"string\"";

                case "integer":
                    return "0";

                case "number":
                    return "0.0";

                case "boolean":
                    return "false";

                default:
                    return "\"...\"";
            }
        }

        private static string GenerateObject(Dictionary<string, object> schema, int depth, Dictionary<string, object> schemas)
        {
            if (schema.TryGetValue("properties", out var props) && props is Dictionary<string, object> propDict)
            {
                var sb = new StringBuilder();
                sb.Append("{");
                bool first = true;
                foreach (var prop in propDict)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append("\n  \"");
                    sb.Append(prop.Key);
                    sb.Append("\": ");
                    if (prop.Value is Dictionary<string, object> ps)
                        sb.Append(GenerateExample(ps, depth + 1, schemas));
                    else
                        sb.Append("\"...\"");
                }
                sb.Append("\n}");
                return sb.ToString();
            }
            return "{}";
        }

        private static string BuildName(string method, string summary, string path)
        {
            if (!string.IsNullOrWhiteSpace(summary))
                return method + " - " + summary;

            var segments = path.Split('/')
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList();

            if (segments.Count == 0)
                return method + " - " + path;

            var name = segments.Last();
            foreach (var seg in Enumerable.Reverse(segments))
            {
                if (!seg.StartsWith("{") && !seg.EndsWith("}"))
                { name = seg; break; }
            }

            var pathParams = segments.Where(s => s.StartsWith("{") && s.EndsWith("}")).ToList();
            if (pathParams.Count > 0)
                name += " (" + string.Join(", ", pathParams.Select(p => p.Trim('{', '}'))) + ")";

            return method + " - " + name;
        }

        public static SavedRequest ConvertToSavedRequest(SwaggerEndpoint ep, string sourceUrl)
        {
            var headerLines = new List<string> { "User-Agent: UltraApiTester/1.0" };

            if (!string.IsNullOrEmpty(ep.RequestBodyMediaType) && ep.RequestBodyMediaType.Contains("json"))
                headerLines.Add("Accept: application/json");
            else
                headerLines.Add("Accept: application/json, text/plain, */*");

            if (!string.IsNullOrEmpty(ep.RequestBodyMediaType))
                headerLines.Add("Content-Type: " + ep.RequestBodyMediaType);

            foreach (var p in ep.Parameters)
                if (p.In == "header")
                    headerLines.Add(p.Name + ": {" + p.Name + "}");

            var headers = string.Join("\r\n", headerLines);

            var url = ep.Path;
            var queryParams = new List<string>();
            foreach (var p in ep.Parameters)
                if (p.In == "query")
                    queryParams.Add(p.Name + "={" + p.Name + "}");
            if (queryParams.Count > 0)
                url += (url.Contains("?") ? "&" : "?") + string.Join("&", queryParams);

            var body = "";
            var method = ep.Method.ToUpper();
            if (method != "GET" && method != "DELETE" && method != "HEAD")
                body = ep.RequestBodyExample ?? "";

            var name = BuildName(method, ep.Summary, ep.Path);
            if (name.Length > 80) name = name.Substring(0, 80);

            return new SavedRequest
            {
                Name = name,
                Group = "",
                Method = method,
                Url = url,
                Headers = headers,
                Body = body,
                Timestamp = DateTime.Now,
                SourceUrl = sourceUrl
            };
        }
    }
}
