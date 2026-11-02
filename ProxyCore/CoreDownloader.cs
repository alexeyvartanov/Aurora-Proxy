using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using Newtonsoft.Json.Linq;

namespace ProxyCore
{
    /// <summary>
    /// 内核下载与安装。
    /// 各内核的发布包结构不同：Xray/V2Ray 的 zip 里 exe 就在根目录或一层子目录，
    /// sing-box 的 zip 名带版本号（必须先在 GitHub API 查最新 tag），
    /// 且 sing-box 使用按标签拆分的 .srs 规则集，与 v2ray 系的 .dat 不通用。
    /// </summary>
    public static class CoreDownloader
    {
        /// <summary>GitHub 镜像，依次尝试。空串表示直连。</summary>
        private static readonly string[] Mirrors = {
            "",
            "https://gh-proxy.org/",
            "https://ghproxy.com/",
            "https://ghproxy.net/",
            "https://mirror.ghproxy.com/"
        };

        static CoreDownloader()
        {
            // 老系统（Win7）默认不开 TLS 1.2，不显式打开就会握手失败
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
        }

        /// <summary>把指定内核下载并安装到 coreDir；返回是否成功。</summary>
        public static bool Install(CoreKind kind, string coreDir, Action<string> log)
        {
            if (log == null) log = s => { };
            Directory.CreateDirectory(coreDir);

            var spec = CoreRegistry.Of(kind);
            log("Подготовка к установке ядра: " + spec.Name);

            string zipRel;
            switch (kind)
            {
                case CoreKind.V2ray:
                    zipRel = "v2fly/v2ray-core/releases/latest/download/v2ray-windows-64.zip";
                    break;
                case CoreKind.Singbox:
                    var ver = FetchLatestSingboxVersion(log);
                    if (string.IsNullOrEmpty(ver))
                    {
                        log("Не удалось узнать последнюю версию sing-box, скачайте sing-box.exe вручную в папку core\\");
                        return false;
                    }
                    log("Последняя версия sing-box: " + ver);
                    zipRel = "SagerNet/sing-box/releases/download/v" + ver + "/sing-box-" + ver + "-windows-amd64.zip";
                    break;
                default:
                    // Win7 只能用专门的 win7 构建（Go 1.20 编译），Win10+ 用标准构建
                    var win7 = Environment.OSVersion.Version.Major < 6 ||
                               (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor <= 1);
                    zipRel = win7
                        ? "XTLS/Xray-core/releases/latest/download/Xray-win7-64.zip"
                        : "XTLS/Xray-core/releases/latest/download/Xray-windows-64.zip";
                    break;
            }

            var tmpZip = Path.Combine(Path.GetTempPath(), "win7proxy-" + spec.ProcessName + ".zip");
            if (!Download("https://github.com/" + zipRel, tmpZip, 500000, log))
            {
                log("Не удалось скачать ядро (испробованы все зеркала).");
                return false;
            }

            var extractDir = Path.Combine(Path.GetTempPath(), "win7proxy-extract-" + spec.ProcessName);
            if (Directory.Exists(extractDir)) try { Directory.Delete(extractDir, true); } catch { }
            Directory.CreateDirectory(extractDir);

            try
            {
                log("Распаковка " + Path.GetFileName(tmpZip));
                ZipFile.ExtractToDirectory(tmpZip, extractDir);
            }
            catch (Exception ex)
            {
                log("Ошибка распаковки: " + ex.Message);
                return false;
            }

            var found = FindFile(extractDir, spec.ExeName);
            if (found == null)
            {
                log("В архиве не найден " + spec.ExeName);
                return false;
            }

            var target = Path.Combine(coreDir, spec.ExeName);
            File.Copy(found, target, true);
            log("Установлен " + spec.ExeName + " (" + new FileInfo(target).Length + " байт)");

            // 附带的可执行文件（v2ray 需要 v2ctl.exe）
            if (kind == CoreKind.V2ray)
            {
                var ctl = FindFile(extractDir, "v2ctl.exe");
                if (ctl != null) { File.Copy(ctl, Path.Combine(coreDir, "v2ctl.exe"), true); log("Установлен v2ctl.exe"); }
            }

            // geo 数据：v2ray 系用 .dat；sing-box 用按标签拆分的 .srs 规则集
            // （sing-box 1.12+ 起 geosite/geoip 改为 .srs 格式，单体 geosite.db/geoip.db 在 1.14.0 已失效）。
            // 逐个检查结果：缺了地理数据规则模式就不可用，必须让用户知道，
            // 而不是"内核安装完成"之后一到规则模式就报错。
            var geoOk = true;
            if (spec.NeedsGeoDb)
            {
                geoOk &= Download("https://raw.githubusercontent.com/SagerNet/sing-geoip/rule-set/" + CoreConstants.GeoIpCnSrs,
                         Path.Combine(coreDir, CoreConstants.GeoIpCnSrs), 5000, log);
                geoOk &= Download("https://raw.githubusercontent.com/SagerNet/sing-geosite/rule-set/" + CoreConstants.GeoSiteCnSrs,
                         Path.Combine(coreDir, CoreConstants.GeoSiteCnSrs), 5000, log);
                geoOk &= Download("https://raw.githubusercontent.com/SagerNet/sing-geosite/rule-set/" + CoreConstants.GeoSiteAdsSrs,
                         Path.Combine(coreDir, CoreConstants.GeoSiteAdsSrs), 5000, log);
            }
            else
            {
                geoOk &= Download("https://github.com/Loyalsoldier/v2ray-rules-dat/releases/latest/download/geoip.dat",
                         Path.Combine(coreDir, CoreConstants.GeoIpFile), 100000, log);
                geoOk &= Download("https://github.com/Loyalsoldier/v2ray-rules-dat/releases/latest/download/geosite.dat",
                         Path.Combine(coreDir, CoreConstants.GeoSiteFile), 100000, log);
            }
            if (!geoOk)
                log("Внимание: гео-правила обновились не полностью (старые файлы сохранены). Повторите «Обновить ядро» позже.");

            return true;
        }

        /// <summary>查询 sing-box 最新版本号（去掉前导 v）；失败返回 null。</summary>
        public static string FetchLatestSingboxVersion(Action<string> log)
        {
            const string api = "https://api.github.com/repos/SagerNet/sing-box/releases/latest";
            // API 直连通常最快且镜像未必代理 api 域名，所以先试直连
            foreach (var prefix in new[] { "", "https://gh-proxy.org/", "https://ghproxy.com/" })
            {
                try
                {
                    using (var wc = new TimeoutWebClient(30000))
                    {
                        wc.Headers.Add("User-Agent", "Win7Proxy");
                        wc.Headers.Add("Accept", "application/vnd.github+json");
                        var json = wc.DownloadString(prefix + api);
                        var tag = (string)JObject.Parse(json)["tag_name"];
                        if (!string.IsNullOrEmpty(tag)) return tag.TrimStart('v', 'V');
                    }
                }
                catch (Exception ex)
                {
                    log?.Invoke("  Не удалось узнать версию (" + (prefix == "" ? "Напрямую" : prefix) + "): " + ex.Message);
                }
            }
            return null;
        }

        /// <summary>
        /// 依次尝试各镜像下载文件，成功返回 true。
        /// 先写到 ".part" 临时文件，校验通过后才替换目标文件：
        /// 旧实现会在尝试前先删掉目标文件，一旦所有镜像都失败，
        /// 原本可用的 geoip.dat / geosite.dat 就被删了，规则模式随之报错。
        /// </summary>
        private static bool Download(string url, string outFile, long minBytes, Action<string> log)
        {
            var tmp = outFile + ".part";

            foreach (var m in Mirrors)
            {
                var full = m + url;
                log("Попытка: " + full);
                try
                {
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                    using (var wc = new TimeoutWebClient(120000))
                    {
                        wc.Headers.Add("User-Agent", "Win7Proxy");
                        wc.DownloadFile(full, tmp);
                    }
                    var sz = File.Exists(tmp) ? new FileInfo(tmp).Length : 0;
                    if (sz >= minBytes)
                    {
                        ReplaceFile(tmp, outFile);
                        log("  ОК: " + Path.GetFileName(outFile) + " (" + sz + " байт)");
                        return true;
                    }
                    log("  Неверный размер (" + sz + " байт), пробуем следующее зеркало");
                }
                catch (Exception ex)
                {
                    log("  Ошибка: " + ex.Message);
                }
            }

            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            log("Все зеркала недоступны: " + url);
            return false;
        }

        /// <summary>用下载好的临时文件替换目标文件；失败时目标文件保持原样。</summary>
        private static void ReplaceFile(string source, string target)
        {
            if (File.Exists(target)) File.Delete(target);
            try { File.Move(source, target); }
            catch
            {
                // 极端情况下 Move 失败（跨卷、目标被占用），退化为复制，保证目标文件可用。
                File.Copy(source, target, true);
                try { File.Delete(source); } catch { }
            }
        }

        private static string FindFile(string dir, string fileName)
        {
            try
            {
                var files = Directory.GetFiles(dir, fileName, SearchOption.AllDirectories);
                if (files.Length > 0) return files[0];
            }
            catch { }
            return null;
        }

        private sealed class TimeoutWebClient : WebClient
        {
            private readonly int _timeoutMs;

            public TimeoutWebClient(int timeoutMs) { _timeoutMs = timeoutMs; }

            protected override WebRequest GetWebRequest(Uri address)
            {
                var request = base.GetWebRequest(address);
                if (request != null) request.Timeout = _timeoutMs;
                var http = request as HttpWebRequest;
                if (http != null) http.ReadWriteTimeout = _timeoutMs;
                return request;
            }
        }
    }
}
