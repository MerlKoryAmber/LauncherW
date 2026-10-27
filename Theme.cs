using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
public static class Theme {
 public static readonly Color Brand=Color.FromArgb(0,130,153), Background=Color.FromArgb(245,245,247), Ink=Color.FromArgb(29,34,37), Muted=Color.FromArgb(94,108,113);
 public static void Apply(Form f){f.Font=new Font("Segoe UI",10);f.BackColor=Background;f.ForeColor=Ink;f.AutoScaleMode=AutoScaleMode.Dpi;var icon=WindowIcon();if(icon!=null)f.Icon=icon;}
 static readonly System.Collections.Generic.List<MemoryStream> IconStreams = new System.Collections.Generic.List<MemoryStream>();
 static Icon WindowIcon(){
  string file=Path.GetFileNameWithoutExtension(Application.ExecutablePath).Equals("Launcher", StringComparison.OrdinalIgnoreCase)?"CorporateApps.ico":"Configurator.ico";
  var embedded=LoadEmbedded(file)??LoadEmbedded("CorporateApps.ico")??LoadEmbedded("Configurator.ico");
  if(embedded!=null)return embedded;
  string dir=AppDomain.CurrentDomain.BaseDirectory;
  string path=Path.Combine(dir,file);
  if(!File.Exists(path)){var parent=Directory.GetParent(dir.TrimEnd('\\','/'));if(parent!=null)path=Path.Combine(parent.FullName,file);}
  if(File.Exists(path)){try{return new Icon(path);}catch(ArgumentException){}}
  return null;
 }
 static Icon LoadEmbedded(string name){
  var source=Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
  if(source==null)return null;
  var copy=new MemoryStream();
  source.CopyTo(copy);
  source.Dispose();
  copy.Position=0;
  IconStreams.Add(copy);
  try{using(var raw=new Icon(copy))return new Icon(raw, SystemInformation.SmallIconSize);}catch(ArgumentException){return null;}
 }
 public static Label Label(string t,float s,bool b){return new Label{Text=t,AutoSize=true,ForeColor=Ink,Font=new Font("Segoe UI",s,b?FontStyle.Bold:FontStyle.Regular)};}
 public static SoftButton Button(string t,EventHandler a,bool p){var b=new SoftButton{Text=t,Primary=p,Width=132,Height=36,Margin=new Padding(0,0,8,0)};if(a!=null)b.Click+=a;return b;}
 public static GraphicsPath Round(Rectangle r,int n){int d=n*2;var p=new GraphicsPath();p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
}
public class SoftButton:Button {
 public bool Primary{get;set;} bool hover,down;
 public SoftButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Cursor=Cursors.Hand;}
 protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);} protected override void OnMouseLeave(EventArgs e){hover=false;down=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnMouseDown(MouseEventArgs e){down=true;Invalidate();base.OnMouseDown(e);} protected override void OnMouseUp(MouseEventArgs e){down=false;Invalidate();base.OnMouseUp(e);}
 protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();} protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
 protected virtual void PaintCaption(Graphics g){TextRenderer.DrawText(g,Text,Font,new Rectangle(8,2,Width-16,Height-5),!Enabled?Theme.Muted:Primary?Color.White:Theme.Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);}
 protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent==null?Theme.Background:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Color fill=Primary?Theme.Brand:Color.White;if(hover)fill=Primary?Color.FromArgb(0,113,134):Color.FromArgb(237,245,246);if(down)fill=Primary?Color.FromArgb(0,98,116):Color.FromArgb(224,234,237);if(!Enabled)fill=Color.FromArgb(231,234,236);using(var p=Theme.Round(new Rectangle(2,2,Width-5,Height-5),10))using(var b=new SolidBrush(fill))using(var pen=new Pen(Focused?Theme.Brand:Primary?fill:Color.FromArgb(206,216,219),Focused?2:1)){e.Graphics.FillPath(b,p);e.Graphics.DrawPath(pen,p);}PaintCaption(e.Graphics);}
}

