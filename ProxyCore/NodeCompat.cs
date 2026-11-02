using System.Collections.Generic;
using ProxyCore.Models;

namespace ProxyCore
{
    /// <summary>
    /// 节点与内核的兼容性检查。
    /// 各内核支持的传输方式并不一致（Xray 已移除 h2，V2Ray 不支持 REALITY），
    /// 这类不兼容的表现是"内核启动即退出"，用户只看到"启动失败"，很难定位。
    /// 这里在启动前就按内核能力表把问题提示出来。
    /// </summary>
    public static class NodeCompat
    {
        /// <summary>默认按 Xray 判断（向后兼容）。</summary>
        public static List<string> Warnings(Node n)
        {
            return Warnings(n, CoreRegistry.Xray);
        }

        /// <summary>返回该节点在指定内核上的已知兼容性提示；没有问题则返回空列表。</summary>
        public static List<string> Warnings(Node n, CoreSpec spec)
        {
            var list = new List<string>();
            if (n == null) return list;
            if (spec == null) spec = CoreRegistry.Xray;

            var net = NetUtil.NormalizeNetwork(n.Network);
            if (n.Type != NodeType.Hysteria2 && !spec.SupportsNetwork(net))
            {
                var alt = new List<string>();
                foreach (var s in CoreRegistry.All)
                    if (s.SupportsNetwork(net)) alt.Add(s.Name);

                list.Add(spec.Name + " не поддерживает транспорт " + net + ", ядро не запустится"
                    + (alt.Count > 0 ? " (" + string.Join(" / ", alt.ToArray()) + " поддерживают)" : "") + ".");
            }

            if (n.Type == NodeType.Hysteria2 && !spec.Hysteria2)
                list.Add(spec.Name + " не поддерживает Hysteria2, ядро не запустится (поддерживается в Xray и sing-box).");

            if (n.Type == NodeType.Shadowsocks)
            {
                var plugin = SsPluginParser.Parse(n.Extra);
                if (plugin != null && plugin.IsV2rayPlugin && spec.Kind != CoreKind.Singbox)
                    list.Add("Shadowsocks v2ray-plugin сопоставлен с транспортом WebSocket"
                        + (plugin.Tls ? " (TLS)" : "") + "; в Xray/V2Ray нет отдельного поля плагина SIP003.");
            }

            if (n.AllowInsecure && spec.Kind == CoreKind.Xray &&
                NetUtil.NormalizeCertPin(n.PinnedCertSha256) == "")
                list.Add("В Xray удалён параметр allowInsecure (пропуск проверки сертификата), будет использована строгая проверка; "
                    + "Для самоподписанного сертификата укажите pinSHA256 или используйте sing-box.");

            if (!string.IsNullOrEmpty(n.PinnedCertSha256) && spec.Kind == CoreKind.Singbox)
                list.Add("sing-box поддерживает только pin публичного ключа и не распознаёт отпечаток сертификата pinSHA256 для hysteria2; параметр будет пропущен.");

            if (n.Type == NodeType.Vmess && !string.IsNullOrEmpty(n.Security) &&
                (n.Security == "none" || n.Security == "zero"))
                list.Add("У этого узла vmess отключено шифрование (" + n.Security + "), трафик легко распознаётся и не рекомендуется большинством ядер.");

            if (string.IsNullOrEmpty(n.Address))
                list.Add("У узла не указан адрес (Address пуст), невозможно создать конфигурацию.");

            if (n.Port <= 0 || n.Port > 65535)
                list.Add("Некорректный порт узла: " + n.Port);

            return list;
        }
    }
}
