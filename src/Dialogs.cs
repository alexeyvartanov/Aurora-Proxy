using System;
using System.Drawing;
using System.Windows.Forms;

namespace AuroraProxy {
 public class SubscriptionDialog:Form {
  TextBox name,url;CheckBox proxy;string suggestedUrl="";
  public string NameValue{get{return name.Text.Trim();}}public string UrlValue{get{return url.Text.Trim();}}public bool ThroughProxy{get{return proxy.Checked;}}
  public SubscriptionDialog(Subscription s,bool running){SuspendLayout();AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;Text=s==null?"Добавить подписку":"Изменить подписку";Font=Theme.Font(9);ClientSize=new Size(570,325);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
   var n=new Label(){Text="Имя (пусто — определить автоматически)",Location=new Point(28,24),AutoSize=true};name=new TextBox(){Text=s==null?"":s.Name,Location=new Point(28,48),Width=510};
   if(s==null)try{string text=Clipboard.ContainsText()?Clipboard.GetText().Trim():"";Uri candidate;if(text.Length<=4096&&Uri.TryCreate(text,UriKind.Absolute,out candidate)&&(candidate.Scheme=="http"||candidate.Scheme=="https"))suggestedUrl=text;}catch{}
   var u=new Label(){Text="Ссылка на подписку Clash / mihomo",Location=new Point(28,94),AutoSize=true};url=new TextBox(){Text=s==null?suggestedUrl:s.Url,Location=new Point(28,119),Width=510,UseSystemPasswordChar=true};
   var reveal=new SoftCheckBox(){Text="Показать ссылку",Location=new Point(28,158),AutoSize=true};reveal.CheckedChanged+=delegate{url.UseSystemPasswordChar=!reveal.Checked;};proxy=new SoftCheckBox(){Text="Загрузить через запущенный локальный прокси",Location=new Point(28,193),AutoSize=true,Enabled=running};
   var note=new Label(){Text=s==null?"Нужна YAML-подписка; ссылки ss / vmess и Base64 не поддерживаются.":"Новая ссылка сразу скачается и проверится; если очистить — конфиг станет локальным.",Location=new Point(28,230),AutoSize=true,Font=Theme.Font(8),Tag="muted"};
   var ok=new SoftButton(){Text=s==null?"Добавить":"Сохранить",Primary=true,Location=new Point(304,270)};ok.Click+=delegate{if(NameValue.Length>80){MessageBox.Show(this,"Имя не длиннее 80 символов; оставьте пустым для автоопределения.");return;}if(s==null&&UrlValue==""){MessageBox.Show(this,"Введите ссылку на подписку.");return;}if(UrlValue!=""){Uri uri;if(!Uri.TryCreate(UrlValue,UriKind.Absolute,out uri)||(uri.Scheme!="https"&&uri.Scheme!="http")){MessageBox.Show(this,"Введите ссылку HTTP / HTTPS.");return;}}DialogResult=DialogResult.OK;};
   var cancel=new SoftButton(){Text="Отмена",Location=new Point(432,270),Width=106,DialogResult=DialogResult.Cancel};Controls.AddRange(new Control[]{n,Theme.Input(name,36),u,Theme.Input(url,36),reveal,proxy,note,ok,cancel});AcceptButton=ok;CancelButton=cancel;Theme.Apply(this);ResumeLayout(true);
  }
 }
 public class DownloadDialog:Form {
  CheckBox proxy;public bool ThroughProxy{get{return proxy.Checked;}}
  public DownloadDialog(bool running){SuspendLayout();AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;Text="Обновить подписку";Font=Theme.Font(9);ClientSize=new Size(430,164);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;
   var l=new Label(){Text="При ошибке обновления текущий конфиг сохранится.",Location=new Point(24,22),AutoSize=true};proxy=new SoftCheckBox(){Text="Загрузить через запущенный локальный прокси",Enabled=running,Location=new Point(24,57),AutoSize=true};var ok=new SoftButton(){Text="Обновить",Primary=true,DialogResult=DialogResult.OK,Location=new Point(165,107)};var cancel=new SoftButton(){Text="Отмена",DialogResult=DialogResult.Cancel,Location=new Point(292,107)};Controls.AddRange(new Control[]{l,proxy,ok,cancel});AcceptButton=ok;CancelButton=cancel;Theme.Apply(this);ResumeLayout(true);
  }
 }
}
