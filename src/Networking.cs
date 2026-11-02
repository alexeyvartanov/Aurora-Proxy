using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;

namespace AuroraProxy {
 public class LanAddress {
  public string Address,Adapter;
  public override string ToString(){return Adapter+"  ·  "+Address;}
 }
 public static class NetworkInfo {
  public static bool PrivateIPv4(IPAddress ip){if(ip.AddressFamily!=AddressFamily.InterNetwork)return false;byte[] b=ip.GetAddressBytes();return b[0]==10||b[0]==172&&b[1]>=16&&b[1]<=31||b[0]==192&&b[1]==168||b[0]==169&&b[1]==254;}
  public static List<LanAddress> Addresses(){var items=new List<LanAddress>();foreach(var nic in NetworkInterface.GetAllNetworkInterfaces()){if(nic.OperationalStatus!=OperationalStatus.Up||nic.NetworkInterfaceType==NetworkInterfaceType.Loopback)continue;try{foreach(var entry in nic.GetIPProperties().UnicastAddresses)if(PrivateIPv4(entry.Address)&&!items.Any(x=>x.Address==entry.Address.ToString()))items.Add(new LanAddress(){Address=entry.Address.ToString(),Adapter=nic.Name});}catch(NetworkInformationException){}}return items;}
  public static string Instructions(string ip,Settings settings,bool running){return "Подключение к прокси через локальную сеть\r\n\r\nСостояние доступа: "+(settings.AllowLan?(running?"работает":"включён в настройках, ядро не запущено"):"не включён")+"\r\nСервер: "+ip+"\r\nПорт: "+settings.Port+"\r\nТип: HTTP / SOCKS5 (один смешанный порт)\r\nАвторизация: "+(settings.LanAuthentication?"имя пользователя "+settings.LanUser+"; пароль — пароль доступа со страницы «Локальная сеть»":"без имени пользователя и пароля")+"\r\n\r\n1. На странице «Локальная сеть» включите общий доступ, сохраните настройки и запустите ядро.\r\n2. Подключите другие устройства к тому же роутеру / Wi-Fi и укажите сервер и порт, приведённые выше; не указывайте 127.0.0.1.\r\n3. На телефоне ручной HTTP-прокси задаётся в настройках Wi-Fi; если там нет полей для имени пользователя / пароля, используйте клиент с поддержкой авторизации прокси.\r\n4. В Windows HTTP-прокси задаётся в «Свойства обозревателя» → «Подключения» → «Настройка сети»; приложения также можно настроить на SOCKS5 по отдельности.\r\n5. Если подключиться не удаётся, разрешите core\\mihomo.exe в брандмауэре Windows этого компьютера для домашней / рабочей сети и проверьте, не включена ли на роутере изоляция устройств.\r\n\r\nЭта версия раздаёт прокси только для частных IPv4-сетей. Компьютер и ядро должны оставаться запущенными; после использования отключите прокси на удалённых устройствах. API управления по-прежнему слушает только локально.\r\nПосле изменения настроек общего доступа остановите ядро, сохраните настройки и запустите его снова.";}
 }
 public static class Startup {
  const string KeyPath=@"Software\Microsoft\Windows\CurrentVersion\Run";
  public static string Command(string executable){if(String.IsNullOrWhiteSpace(executable)||executable.IndexOf('"')>=0)throw new ArgumentException("Недопустимый путь к программе.");return "\""+executable+"\" --tray";}
  public static void SetEnabled(bool enabled,string executable){if(!SystemProxy.Windows){if(enabled)throw new PlatformNotSupportedException("Автозапуск доступен только в Windows.");return;}using(var key=Registry.CurrentUser.CreateSubKey(KeyPath)){if(enabled)key.SetValue("AuroraProxy",Command(executable),RegistryValueKind.String);else key.DeleteValue("AuroraProxy",false);}}
 }
 public class ConnectionInfo {
  public string Id,Target,Source,Protocol,Rule;public long Upload,Download;
  static string Text(Dictionary<string,object> data,string key){object o;return data.TryGetValue(key,out o)&&o!=null?o.ToString():"";}
  static long Number(Dictionary<string,object> data,string key){long n;return Int64.TryParse(Text(data,key),out n)?n:0;}
  public static List<ConnectionInfo> Parse(Dictionary<string,object> data){var items=new List<ConnectionInfo>();object all;if(!data.TryGetValue("connections",out all))return items;var list=all as IEnumerable;if(list==null||all is string)return items;foreach(var value in list){var d=value as Dictionary<string,object>;if(d==null)continue;object raw;var m=d.TryGetValue("metadata",out raw)?raw as Dictionary<string,object>:null;if(m==null)m=new Dictionary<string,object>();string host=Text(m,"host");if(host=="")host=Text(m,"destinationIP");items.Add(new ConnectionInfo(){Id=Text(d,"id"),Target=host+":"+Text(m,"destinationPort"),Source=Text(m,"sourceIP")+":"+Text(m,"sourcePort"),Protocol=Text(m,"network")+" · "+Text(m,"type"),Rule=Text(d,"rule")+(Text(d,"rulePayload")==""?"":" · "+Text(d,"rulePayload")),Upload=Number(d,"upload"),Download=Number(d,"download")});}return items;}
 }
}
