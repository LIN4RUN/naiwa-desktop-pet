using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NaiwaDesktop
{
    internal static class Native
    {
        internal const string Title="NaiwaDesktop.Source.v1";
        internal const int Wake=0x8001, Status=0x8002, Hungry=0x8003;   // v1.8.38：Hungry=0x8003 减饥饿值消息（文件夹内 bat 调用）
        [StructLayout(LayoutKind.Sequential)] internal struct Point { internal int X,Y; internal Point(int x,int y){X=x;Y=y;} }
        [StructLayout(LayoutKind.Sequential)] internal struct Size { internal int W,H; internal Size(int w,int h){W=w;H=h;} }
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { internal int Left,Top,Right,Bottom; }
        [StructLayout(LayoutKind.Sequential,Pack=1)] internal struct Blend { internal byte Op,Flags,Alpha,Format; }
        [StructLayout(LayoutKind.Sequential)] internal struct BitmapInfo
        {
            internal uint Size; internal int Width,Height; internal ushort Planes,BitCount;
            internal uint Compression,SizeImage; internal int XPels,YPels; internal uint Used,Important;
        }
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr FindWindow(string cls,string text);
        [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr h,int m,IntPtr w,IntPtr l);
        [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern int GetClassName(IntPtr h,StringBuilder name,int count);
        [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")] internal static extern int DwmRect(IntPtr h,int attr,out Rect value,int size);
        [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")] internal static extern int DwmInt(IntPtr h,int attr,out int value,int size);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr h,out Rect r);
        [DllImport("user32.dll")] internal static extern int GetWindowLong(IntPtr h,int index);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr h,out uint id);
        [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr h,IntPtr dc);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int vKey);
        [DllImport("gdi32.dll")] internal static extern IntPtr CreateCompatibleDC(IntPtr h);
        [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr h);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr h);
        [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
        [DllImport("gdi32.dll")] internal static extern IntPtr CreateDIBSection(IntPtr h,ref BitmapInfo info,uint usage,out IntPtr bits,IntPtr section,uint offset);
        [DllImport("user32.dll",SetLastError=true)] internal static extern bool UpdateLayeredWindow(IntPtr h,IntPtr dest,ref Point position,ref Size size,IntPtr src,ref Point origin,uint key,ref Blend blend,uint flags);
        [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")] internal static extern int GetGuiResources(IntPtr process,int flags);
    }

    internal static class Program
    {
        [STAThread] private static void Main(string[] args)
        {
            Native.SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length>=2 && args[0]=="--self-test") { Tests.Run(args[1]); return; }
            if(args.Length>=2 && args[0]=="--demo") { Tests.Demo(args[1]); return; }
            if(args.Length>=2 && args[0]=="--drop-test") { Tests.DropTest(args[1]); return; }
            if(args.Length>=2 && args[0]=="--probe")
            {
                IntPtr h=Native.FindWindow(null,Native.Title); Native.Rect r; Native.GetWindowRect(h,out r);
                File.WriteAllText(args[1],"Found="+(h!=IntPtr.Zero)+"\r\nVisible="+Native.IsWindowVisible(h)+"\r\nBounds="+r.Left+","+r.Top+","+r.Right+","+r.Bottom+"\r\nState="+Native.SendMessage(h,Native.Status,IntPtr.Zero,IntPtr.Zero),Encoding.UTF8); return;
            }
            if(args.Length==1 && args[0]=="--quit") { Native.PostMessage(Native.FindWindow(null,Native.Title),0x10,IntPtr.Zero,IntPtr.Zero); return; }
            if(args.Length==1 && args[0]=="--hungry") { IntPtr hw=Native.FindWindow(null,Native.Title); bool hok=Native.PostMessage(hw,Native.Hungry,IntPtr.Zero,IntPtr.Zero); try{File.AppendAllText(Path.Combine(Settings.Folder,"hungry.log"),DateTime.Now.ToString("s")+" SEND hwnd="+hw+" ok="+hok+Environment.NewLine);}catch{} return; }   // v1.8.38：文件夹"饿一点.bat" → 减 1 点饥饿（只减不加）
            bool fresh;
            using(Mutex mutex=new Mutex(true,"Local\\NaiwaDesktop.Source.v1",out fresh))
            {
                if(!fresh){Native.PostMessage(Native.FindWindow(null,Native.Title),Native.Wake,IntPtr.Zero,IntPtr.Zero);return;}
                Application.ThreadException+=delegate(object s,ThreadExceptionEventArgs e){Report(e.Exception); Application.Exit();};
                try { using(Assets assets=new Assets(AppDomain.CurrentDomain.BaseDirectory)) using(PetWindow pet=new PetWindow(assets,false)){Application.Run(pet);} }
                catch(Exception e){Report(e);}
            }
        }
        private static void Report(Exception e)
        {
            try{Directory.CreateDirectory(Settings.Folder);File.AppendAllText(Path.Combine(Settings.Folder,"error.log"),DateTime.Now.ToString("s")+" "+e+Environment.NewLine);}catch{}
            MessageBox.Show("奶蛙暂时没能启动。请将程序、素材.nwp 和原版笑声.wav 放在同一个文件夹。\n\n"+e.Message,"奶蛙桌宠",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
    }

    internal sealed class EyeArt:IDisposable
    {
        internal readonly Bitmap Blank,IrisL,IrisR;
        internal readonly int Lx,Ly,Rx,Ry;internal readonly double RxR,RyR;
        internal EyeArt(Bitmap blank,Bitmap l,Bitmap r,int lx,int ly,int rx,int ry,double rxx,double ryy){Blank=blank;IrisL=l;IrisR=r;Lx=lx;Ly=ly;Rx=rx;Ry=ry;RxR=rxx;RyR=ryy;}
        public void Dispose(){if(Blank!=null)Blank.Dispose();if(IrisL!=null)IrisL.Dispose();if(IrisR!=null)IrisR.Dispose();}
    }

    // ---- v1.6：回收文件动作素材（drag.nwp，缺省则拖入只提示不播动画）----
    internal sealed class DragArt:IDisposable
    {
        internal readonly int Count,Width,Height;internal readonly double Fps,Duration;
        private readonly byte[][] encoded;
        private Bitmap[] frames;
        internal bool Ready{get{return frames!=null;}}
        internal DragArt(string folder)
        {
            string path=Path.Combine(folder,"drag.nwp");
            if(!File.Exists(path)){Count=0;Width=Height=0;Fps=15;Duration=0;encoded=null;return;}
            using(ZipArchive zip=ZipFile.OpenRead(path))
            {
                Dictionary<string,object> meta;
                using(StreamReader reader=new StreamReader(zip.GetEntry("metadata.json").Open())) meta=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(reader.ReadToEnd());
                Count=Convert.ToInt32(meta["frames"]);Fps=Convert.ToDouble(meta["fps"]);Duration=Count/Fps;
                Width=(meta.ContainsKey("width")?Convert.ToInt32(meta["width"]):0)/2;
                Height=(meta.ContainsKey("height")?Convert.ToInt32(meta["height"]):0)/2;
                if(Count<1||Count>600||Fps<1||Fps>120)throw new InvalidDataException("拖放素材信息无效");
                encoded=new byte[Count][];
                for(int i=0;i<Count;i++)encoded[i]=Assets.Read(zip,"drag/"+i.ToString("0000")+".png");
            }
        }
        internal void Ensure(Assets a)
        {
            if(frames!=null||encoded==null)return;
            // v1.7.2：素材为 2x 高清，解压保持原始 2x 尺寸，渲染时缩小绘制（高清）
            int w=Width>0?Width*2:a.Width*2,h=Height>0?Height*2:a.Height*2;
            frames=new Bitmap[Count];
            try{for(int i=0;i<Count;i++)frames[i]=a.DecodeTo(encoded[i],w,h);}
            catch{for(int i=0;i<Count;i++)if(frames[i]!=null)frames[i].Dispose();frames=null;}
        }
        internal Bitmap Frame(int index){return frames[Math.Max(0,Math.Min(Count-1,index))];}
        public void Dispose(){if(frames!=null)foreach(Bitmap b in frames)if(b!=null)b.Dispose();frames=null;}
    }

    // ---- v1.6.4：拖放完成后的沮丧表情素材（feed.nwp，单帧，缺省则直接回待机）----
    internal sealed class FeedArt:IDisposable
    {
        internal readonly int Width,Height;
        private byte[] data;private Bitmap frame;
        internal bool Ready{get{return frame!=null;}}
        internal FeedArt(string folder)
        {
            string path=Path.Combine(folder,"feed.nwp");
            if(!File.Exists(path)){Width=Height=0;data=null;return;}
            using(ZipArchive zip=ZipFile.OpenRead(path))
            {
                Dictionary<string,object> meta;
                using(StreamReader reader=new StreamReader(zip.GetEntry("metadata.json").Open())) meta=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(reader.ReadToEnd());
                Width=Convert.ToInt32(meta["width"])/2;Height=Convert.ToInt32(meta["height"])/2;
                data=Assets.Read(zip,"feed/0000.png");
            }
        }
        internal void Ensure(Assets a)
        {
            if(frame!=null||data==null)return;
            try{frame=a.DecodeTo(data,Width>0?Width*2:a.Width*2,Height>0?Height*2:a.Height*2);}
            catch{data=null;}
        }
        internal Bitmap Frame(){return frame;}
        public void Dispose(){if(frame!=null)frame.Dispose();frame=null;}
    }

    // ---- v1.8：行走动画素材（walk.nwp，8 帧，拖动奶蛙时播放）----
    internal sealed class WalkArt:IDisposable
    {
        internal readonly int Count,Width,Height;internal readonly double Fps;
        private readonly byte[][] encoded;
        private Bitmap[] frames;
        internal bool Ready{get{return frames!=null;}}
        internal WalkArt(string folder,string name)
        {
            string path=Path.Combine(folder,name);
            if(!File.Exists(path)){Count=0;Width=Height=0;Fps=10;encoded=null;return;}
            using(ZipArchive zip=ZipFile.OpenRead(path))
            {
                Dictionary<string,object> meta;
                using(StreamReader reader=new StreamReader(zip.GetEntry("metadata.json").Open())) meta=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(reader.ReadToEnd());
                Count=Convert.ToInt32(meta["frames"]);Fps=Convert.ToDouble(meta["fps"]);
                Width=(meta.ContainsKey("width")?Convert.ToInt32(meta["width"]):0)/2;
                Height=(meta.ContainsKey("height")?Convert.ToInt32(meta["height"]):0)/2;
                if(Count<1||Count>120||Fps<1||Fps>60)throw new InvalidDataException("行走素材信息无效");
                encoded=new byte[Count][];
                string prefix=name.Substring(0,name.IndexOf('.'));
                for(int i=0;i<Count;i++)encoded[i]=Assets.Read(zip,prefix+"/"+i.ToString("0000")+".png");
            }
        }
        internal void Ensure(Assets a)
        {
            if(frames!=null||encoded==null)return;
            int w=Width>0?Width*2:a.Width*2,h=Height>0?Height*2:a.Height*2;
            frames=new Bitmap[Count];
            try{for(int i=0;i<Count;i++)frames[i]=a.DecodeTo(encoded[i],w,h);}
            catch{for(int i=0;i<Count;i++)if(frames[i]!=null)frames[i].Dispose();frames=null;}
        }
        internal Bitmap Frame(int index){return frames[Math.Max(0,Math.Min(Count-1,index))];}
        public void Dispose(){if(frames!=null)foreach(Bitmap b in frames)if(b!=null)b.Dispose();frames=null;}
    }

    internal sealed class Assets:IDisposable
    {
        internal readonly int Width,Height,Count,EyeCount,BlinkCount;
        internal readonly double Fps,Duration,BlinkFps;
        private readonly byte[][] encoded;
        private readonly Bitmap[] eyes;
        private readonly Bitmap[][] blinks;
        private readonly Dictionary<int,Bitmap> cache=new Dictionary<int,Bitmap>();
        private readonly Queue<int> order=new Queue<int>();
        internal readonly string AudioPath;
        // ---- v1.5：360° 眼珠素材（eye_art.nwp，缺省则回退原 look 帧）----
        internal readonly EyeArt Eyes;
        // ---- v1.6：回收文件动作素材 ----
        internal readonly DragArt Drag;
        // ---- v1.6.4：沮丧表情素材 ----
        internal readonly FeedArt Feed;
        // ---- v1.8：行走动画素材 ----
        internal readonly WalkArt Walk;
        // ---- v1.8.9：拖起奶蛙（惊讶表情）素材 ----
        internal readonly WalkArt Hold;
        // ---- v1.8.14：右贴边隐藏时露出的头部素材 ----
        internal readonly WalkArt Head;
        // ---- v1.8.17：双击头部三阶段动作素材（起势/愤怒/打电话）----
        internal readonly WalkArt Seq;
        // ---- v1.8.24：爆炸后小奶蛙地面舞蹈素材（张嘴之后的舞蹈段）----
        internal readonly WalkArt Dance;
        // ---- v1.8.37：饥饿表情素材（跪地祈祷，hunger<3 扣点时显示 10 秒）----
        internal readonly WalkArt Hunger;
        internal readonly WalkArt Fat;   // v1.8.39：吃饱撑肚（饥饿<3喂满后冒烟变成胖袋鼠）
        internal readonly WalkArt Rest;   // v1.8.40：睡觉"笑倒"GIF 动画
        internal readonly WalkArt Rest2;   // v1.8.40：睡觉定格——躺卧照片
        internal int CacheCount{get{return cache.Count;}}
        internal Assets(string folder)
        {
            AudioPath=Path.Combine(folder,"原版笑声.wav");
            using(ZipArchive zip=ZipFile.OpenRead(Path.Combine(folder,"素材.nwp")))
            {
                Dictionary<string,object> meta;
                using(StreamReader reader=new StreamReader(zip.GetEntry("metadata.json").Open())) meta=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(reader.ReadToEnd());
                Width=Convert.ToInt32(meta["width"])/2;Height=Convert.ToInt32(meta["height"])/2;Count=Convert.ToInt32(meta["frames"]);Fps=Convert.ToDouble(meta["fps"]);EyeCount=Convert.ToInt32(meta["eyeFrames"]);Duration=Count/Fps;
                if(Width<1||Height<1||Width>1024||Height>1024||Count<1||Count>2000||Fps<1||Fps>120||EyeCount<2||EyeCount>60)throw new InvalidDataException("素材信息无效");
                eyes=new Bitmap[EyeCount];
                for(int i=0;i<EyeCount;i++)eyes[i]=Decode(Read(zip,"look/"+i.ToString("00")+".png"));
                BlinkCount=Convert.ToInt32(meta["blinkFrames"]);BlinkFps=Convert.ToDouble(meta["blinkFps"]);
                if(BlinkCount<2||BlinkCount>60||BlinkFps<1||BlinkFps>120)throw new InvalidDataException("眨眼素材信息无效");
                blinks=new Bitmap[2][];
                for(int mode=0;mode<2;mode++){blinks[mode]=new Bitmap[BlinkCount];for(int i=0;i<BlinkCount;i++)blinks[mode][i]=Decode(Read(zip,"blink/"+mode+"/"+i.ToString("00")+".png"));}
                encoded=new byte[Count][];
                for(int i=0;i<Count;i++)encoded[i]=Read(zip,"laugh/"+i.ToString("0000")+".png");
            }
            Eyes=LoadEyeArt(folder);
            Drag=new DragArt(folder);
            Feed=new FeedArt(folder);
            Walk=new WalkArt(folder,"walk.nwp");   // v1.8：行走动画
            Hold=new WalkArt(folder,"hold.nwp");   // v1.8.9：拖起奶蛙（惊讶表情）
            Head=new WalkArt(folder,"head.nwp");   // v1.8.14：右贴边头部素材
            Seq=new WalkArt(folder,"seq.nwp");   // v1.8.17：双击头部三阶段动作
            Dance=new WalkArt(folder,"dance.nwp");   // v1.8.24：爆炸后地面舞蹈
            Hunger=new WalkArt(folder,"hunger.nwp");   // v1.8.37：饥饿表情
            Fat=new WalkArt(folder,"fat.nwp");   // v1.8.39：吃饱撑肚
            Rest=new WalkArt(folder,"rest.nwp");   // v1.8.40：睡觉"笑倒"GIF 动画
            Rest2=new WalkArt(folder,"rest2.nwp");   // v1.8.40：睡觉定格——躺卧照片
            // Warm up the first frames before the first double click.
            Frame(0);Frame(1);Frame(2);
        }
        private static EyeArt LoadEyeArt(string folder)
        {
            try
            {
                string path=Path.Combine(folder,"eye_art.nwp");
                if(!File.Exists(path))return null;
                using(ZipArchive az=ZipFile.OpenRead(path))
                {
                    Dictionary<string,object> am;
                    using(StreamReader sr=new StreamReader(az.GetEntry("metadata.json").Open())) am=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(sr.ReadToEnd());
                    Bitmap blank=DecodeAny(Read(az,(string)am["blank"])),il=DecodeAny(Read(az,(string)am["left"])),ir=DecodeAny(Read(az,(string)am["right"]));
                    System.Collections.ArrayList lc=(System.Collections.ArrayList)am["leftCenter"],rc=(System.Collections.ArrayList)am["rightCenter"];
                    double rxx=2.5,ryy=2.0;
                    if(am.ContainsKey("radiusX"))rxx=Convert.ToDouble(am["radiusX"]);
                    if(am.ContainsKey("radiusY"))ryy=Convert.ToDouble(am["radiusY"]);
                    if(am.ContainsKey("radius"))rxx=ryy=Convert.ToDouble(am["radius"]);
                    return new EyeArt(blank,il,ir,Convert.ToInt32(lc[0]),Convert.ToInt32(lc[1]),Convert.ToInt32(rc[0]),Convert.ToInt32(rc[1]),rxx,ryy);
                }
            }
            catch{return null;}
        }
        private static Bitmap DecodeAny(byte[] data)
        {
            using(MemoryStream stream=new MemoryStream(data)) using(Image image=Image.FromStream(stream))
            {
                Bitmap b=new Bitmap(image.Width,image.Height,PixelFormat.Format32bppPArgb);
                using(Graphics g=Graphics.FromImage(b)){g.CompositingMode=CompositingMode.SourceCopy;g.DrawImageUnscaled(image,0,0);}return b;
            }
        }
        internal static byte[] Read(ZipArchive zip,string name)
        { ZipArchiveEntry entry=zip.GetEntry(name);if(entry==null)throw new InvalidDataException("缺少动作素材："+name);using(Stream s=entry.Open())using(MemoryStream m=new MemoryStream()){s.CopyTo(m);return m.ToArray();} }
        // v1.7.2：素材为 2x 高清，解压保持原始 2x 尺寸，渲染时缩小绘制（高清）
        private Bitmap Decode(byte[] data){return DecodeTo(data,Width*2,Height*2);}
        internal Bitmap DecodeTo(byte[] data,int w,int h)
        {
            using(MemoryStream stream=new MemoryStream(data)) using(Image image=Image.FromStream(stream))
            {
                if(image.Width!=w||image.Height!=h)throw new InvalidDataException("动作尺寸不一致");
                Bitmap b=new Bitmap(w,h,PixelFormat.Format32bppPArgb);
                using(Graphics g=Graphics.FromImage(b)){g.CompositingMode=CompositingMode.SourceCopy;g.DrawImageUnscaled(image,0,0);}return b;
            }
        }
        internal Bitmap Eye(double value){return eyes[Math.Max(0,Math.Min(EyeCount-1,(int)Math.Round(value*(EyeCount-1))))];}
        internal Bitmap Blink(double look,int frame){return blinks[look>.5?1:0][Math.Max(0,Math.Min(BlinkCount-1,frame))];}
        internal Bitmap Frame(int index)
        {
            index=Math.Max(0,Math.Min(Count-1,index));Bitmap b;
            if(cache.TryGetValue(index,out b))return b;
            b=Decode(encoded[index]);cache.Add(index,b);order.Enqueue(index);
            while(order.Count>12){int old=order.Dequeue();cache[old].Dispose();cache.Remove(old);}return b;
        }
        public void Dispose(){if(eyes!=null)foreach(Bitmap b in eyes)if(b!=null)b.Dispose();if(blinks!=null)foreach(Bitmap[] bank in blinks)foreach(Bitmap b in bank)if(b!=null)b.Dispose();foreach(Bitmap b in cache.Values)b.Dispose();cache.Clear();if(Eyes!=null)Eyes.Dispose();if(Drag!=null)Drag.Dispose();if(Feed!=null)Feed.Dispose();if(Walk!=null)Walk.Dispose();if(Hold!=null)Hold.Dispose();if(Head!=null)Head.Dispose();if(Seq!=null)Seq.Dispose();if(Dance!=null)Dance.Dispose();if(Hunger!=null)Hunger.Dispose();if(Fat!=null)Fat.Dispose();if(Rest!=null)Rest.Dispose();if(Rest2!=null)Rest2.Dispose();}
    }

    internal sealed class Motion
    {
        private const double PokeDuration=.95;
        internal double Look,Start=-1,Elapsed,Clock,BlinkStart=-1,NextBlink=4,WakeStart=-1;
        internal double PokeStart=-1,PokeX=144,PokeY=205,LandingStart=-1,DragAngle,WalkStart=-1;
        internal int WalkDir=1;
        internal bool Resting,Holding;
        internal bool Playing{get{return Start>=0;}}
        internal bool Blinking{get{return BlinkStart>=0;}}
        internal bool Poking{get{return PokeStart>=0;}}
        internal bool Landing{get{return LandingStart>=0;}}
        internal bool Waking{get{return WakeStart>=0;}}
        internal bool Moving{get{return Holding||Landing||Math.Abs(DragAngle)>.02;}}
        internal double PokeAmount{get{double t=Clock-PokeStart;if(!Poking||t<0||t>=PokeDuration)return 0;return t<.12?.85*Math.Sin(t/.12*Math.PI/2):.85*Math.Exp(-4.8*(t-.12))*Math.Cos(18*(t-.12));}}
        internal double Squash{get{double t=Clock-LandingStart;return Landing&&t>=0?.055*Math.Exp(-6*t)*Math.Sin(19*t):0;}}
        internal int PlayCount;
        // ---- v1.4 新增：眼睛全局跟随鼠标 ----
        internal bool AutoLook;internal double LookGoal=.5;
        // ---- v1.5 新增：垂直注视（0=向下看，0.5=平视，1=向上看）----
        internal double LookV=.5,LookGoalV=.5;
        // ---- v1.6：回收文件动画 ----
        internal double DragStart=-1,DragDuration=8;
        internal bool HoverOpen;
        internal double HoverStart=-1;
        internal bool Dragging{get{return DragStart>=0;}}
        // ---- v1.6.4：沮丧表情（拖走未放下时触发，3秒）----
        internal double FeedStart=-1;
        internal bool Feeding{get{return FeedStart>=0;}}
        // ---- v1.8.37：饥饿表情（hunger<3 扣点时触发，持续 10 秒）----
        internal double HungryStart=-1;
        internal bool Hungry{get{return HungryStart>=0;}}
        internal void StartHungry(double now){ClearEffects(now);WalkStart=-1;HungryStart=now;}   // v1.8.39：清行走（否则 Walking 分支在前，乞讨画不出来）
        internal void EndHungry(){HungryStart=-1;}
        // ---- v1.8.39：喂食后庆祝舞蹈（dance.nwp 播一遍 6.67s；再次喂食时先取消再张嘴回收）----
        internal double DanceStart=-1;
        internal bool Dancing{get{return DanceStart>=0;}}
        internal void StartDance(double now){ClearEffects(now);WalkStart=-1;DanceStart=now;}
        internal void EndDance(){DanceStart=-1;}
        // ---- v1.8.39：吃饱撑肚（饥饿<3时喂满 → 冒烟1.2s变成胖袋鼠，持续6秒）----
        internal double FatStart=-1;
        internal bool Fat{get{return FatStart>=0;}}
        internal void StartFat(double now){ClearEffects(now);WalkStart=-1;DanceStart=-1;FatStart=now;}
        internal void EndFat(){FatStart=-1;}
        // v1.8.38：重力下落中（惊恐表情；落地由 PetWindow 清掉）
        internal bool Falling;
        internal void StartDrag(double now,double duration){DragStart=now;DragDuration=Math.Max(.5,duration);Look=0;LookV=.5;HoverOpen=false;FeedStart=-1;ClearEffects(now);}
        // ---- v1.3 新增：空闲小动作 / 打盹 / 头部点击 / 庆祝 ----
        internal double IdleStart=-1,IdleEnd;internal int IdleKind=-1;
        internal double SleepStart=-1,HeadStart=-1,CelebrateStart=-1;
        internal bool Sleeping{get{return SleepStart>=0;}}
        internal bool Idling{get{return IdleStart>=0;}}
        internal bool HeadPoking{get{return HeadStart>=0;}}
        internal bool Celebrating{get{return CelebrateStart>=0;}}
        internal bool HasExtra{get{return IdleStart>=0||SleepStart>=0||HeadStart>=0||CelebrateStart>=0||WalkStart>=0||HeadSeqStart>=0||DanceStart>=0||FatStart>=0;}}   // v1.8.17 / v1.8.39：跳舞/吃饱也算占位（不睡/不散步）
        // v1.8.1：重力落地后的行走（WalkStart 与拖动共用时间戳，Holding 时不算行走）
        internal bool Walking{get{return !Holding&&WalkStart>=0;}}
        internal void StartWalk(double now,int dir){ClearAll(now);WalkStart=now;WalkDir=dir>0?1:-1;}
        // ---- v1.8.17：双击眼睛三阶段动作（1起势 → 2愤怒 → 3打电话2秒→爆炸出6只小奶蛙吃东西跳舞；不循环，3 后忽略）----
        internal double HeadSeqStart=-1;internal int HeadSeq=0;
        internal bool HeadSequencing{get{return HeadSeqStart>=0;}}
        internal void StartHeadSeq(double now)
        {
            if(Playing||Resting||Holding||Sleeping||Dragging||HoverOpen||Feeding)return;   // 互斥：动画/拖/睡/张嘴中不打断
            if(HeadSeq>=3)return;   // v1.8.32：第 3 阶段（打电话/炸开舞群）点击无效，不跳回起势；舞群结束自动归位后可重新开始
            int next=HeadSeq>=3?1:HeadSeq+1;   // v1.8.22：先算下一阶段再清动画（否则 ClearAll 清 0 后永远回到 1）
            ClearAll(now);
            HeadSeq=next;
            HeadSeqStart=now;
        }
        internal void EndHeadSeq(){HeadSeqStart=-1;HeadSeq=0;}
        private double previous=-1,dragTarget,dragVelocity,lastDragTime;
        private readonly Random random=new Random();
        internal void Update(double now,bool over,double duration,double blinkDuration)
        {
            Clock=now;double dt=previous<0?0:Math.Min(.05,Math.Max(0,now-previous));previous=now;
            double goal=AutoLook?LookGoal:(over?1:0),step=dt/.18;Look+=Math.Max(-step,Math.Min(step,goal-Look));
            double goalV=AutoLook?LookGoalV:.5;LookV+=Math.Max(-step,Math.Min(step,goalV-LookV));
            if(Playing){Elapsed=Math.Max(0,now-Start);if(Elapsed>=duration){Start=-1;Elapsed=duration;Resting=false;}}   // v1.8.7：笑完直接切回初始待机，不再定格休息
            if(Waking&&now-WakeStart>=.24)WakeStart=-1;
            if(Poking&&now-PokeStart>=PokeDuration)PokeStart=-1;
            if(Landing&&now-LandingStart>=.9)LandingStart=-1;
            if(Blinking&&now-BlinkStart>=blinkDuration){BlinkStart=-1;ScheduleBlink(now);}
            if(Idling&&now>=IdleEnd)IdleStart=-1;
            if(Sleeping&&now-SleepStart>=600)SleepStart=-1;
            if(HeadPoking&&now-HeadStart>=1.0)HeadStart=-1;
            if(Celebrating&&now-CelebrateStart>=2.8)CelebrateStart=-1;
            if(Feeding&&now-FeedStart>=1)FeedStart=-1;
            if(Hungry&&now-HungryStart>=10)HungryStart=-1;   // v1.8.37：饥饿表情持续 10 秒
            if(Dancing&&now-DanceStart>=6.7)DanceStart=-1;   // v1.8.39：庆祝舞蹈播完（dance 100 帧@15fps）自动停
            if(Fat&&now-FatStart>=6)FatStart=-1;   // v1.8.39：吃饱撑肚（冒烟1.2s+胖袋鼠）持续 6 秒后恢复
            if(Walking&&now-WalkStart>=4)WalkStart=-1;
            if(HeadSequencing&&now-HeadSeqStart>=8)EndHeadSeq();   // v1.8.17：三阶段动作/奶蛙群持续 8 秒后恢复待机
            if(!Playing&&!Resting&&!Holding&&!Landing&&!Poking&&!Waking&&!Blinking&&!HasExtra&&now>=NextBlink)BlinkStart=now;
            if(!Holding||now-lastDragTime>.10)dragTarget=0;
            // Critically damped tilt; bounded steps keep long timer gaps stable.
            int steps=Math.Max(1,(int)Math.Ceiling(dt/.008));double stepTime=dt/steps;
            for(int i=0;i<steps;i++){dragVelocity+=(160*(dragTarget-DragAngle)-25*dragVelocity)*stepTime;DragAngle+=dragVelocity*stepTime;}
            DragAngle=Math.Max(-7,Math.Min(7,DragAngle));
            if(!Holding&&Math.Abs(DragAngle)<.02&&Math.Abs(dragVelocity)<.05){DragAngle=0;dragVelocity=0;}
        }
        private void ScheduleBlink(double now){NextBlink=now+4+random.NextDouble()*4;}
        internal void ClearEffects(double now){Clock=now;BlinkStart=PokeStart=WakeStart=LandingStart=IdleStart=SleepStart=HeadStart=CelebrateStart=HungryStart=DanceStart=FatStart=-1;Holding=false;DragAngle=dragTarget=dragVelocity=0;ScheduleBlink(now);}
        // v1.6.8：取消一切动画（含回收/张嘴/沮丧）——新动画启动时互斥覆盖
        // v1.8.18：拖起奶蛙（BeginDrag→ClearAll）同时取消三阶段动作/奶蛙群
        private void ClearAll(double now){DragStart=-1;HoverOpen=false;FeedStart=-1;WalkStart=-1;HeadSeqStart=-1;HeadSeq=0;ClearEffects(now);}
        internal bool Laugh(double now){if(Playing)return false;ClearAll(now);Resting=false;Start=now;Elapsed=0;PlayCount++;return true;}
        internal void Stop(){Start=-1;Elapsed=0;Resting=false;ClearEffects(Clock);}
        internal void Stand(double now){if(!Resting)return;Resting=false;ClearAll(now);WakeStart=now;}
        // v1.8.17：双击触发区=眼睛下方那块区域（脸颊）；v1.8.19 下移；v1.8.20 放大便于触发
        internal static bool InEye(double x,double y){double dx=(x-145)/60,dy=(y-95)/45;return dx*dx+dy*dy<=1&&y>=55;}
        internal static bool InBelly(double x,double y){double dx=(x-107)/75,dy=(y-211)/75;return dx*dx+dy*dy<=1;}
        internal bool Poke(double now,double x,double y){if(Playing||Resting||Holding||!InBelly(x,y))return false;ClearAll(now);PokeStart=now;PokeX=x;PokeY=y;return true;}
        internal void BeginDrag(double now){Start=-1;Elapsed=0;ClearAll(now);Holding=true;WalkStart=now;lastDragTime=now;}
        internal void Drag(double now,double dx){if(!Holding)BeginDrag(now);double dt=Math.Max(1.0/120,now-lastDragTime);lastDragTime=now;dragTarget=Math.Max(-7,Math.Min(7,dx/dt*.013));Clock=now;}
        internal void EndDrag(double now){if(!Holding)return;Holding=false;dragTarget=0;Clock=now;LandingStart=now;ScheduleBlink(now);}
        internal void Suspend(double now){Start=-1;Elapsed=0;Look=0;LookV=.5;DragStart=-1;HoverOpen=false;FeedStart=-1;ClearEffects(now);}
        // ---- v1.3 新增状态入口 ----
        internal void StartIdle(double now,int kind,int durationMs){ClearAll(now);IdleStart=now;IdleEnd=now+durationMs/1000.0;IdleKind=kind;}
        internal bool Sleep(double now){if(Playing||Resting||Holding||Poking||HeadPoking||Celebrating||Dragging||HoverOpen||Feeding)return false;ClearAll(now);SleepStart=now;return true;}
        internal bool Wake(double now){if(!Sleeping)return false;SleepStart=-1;BlinkStart=now;ScheduleBlink(now);return true;}
        internal bool HeadPoke(double now){if(Playing||Resting||Holding||Sleeping||Celebrating||Dragging||HoverOpen||Feeding)return false;ClearAll(now);HeadStart=now;return true;}
        internal void Celebrate(double now){ClearAll(now);CelebrateStart=now;Resting=false;}
        internal static bool InHead(double x,double y){double dx=(x-145)/56,dy=(y-62)/52;return dx*dx+dy*dy<=1&&y>=10;}
    }

    internal static class Fullscreen
    {
        internal static bool Covers(Rectangle window,Rectangle screen,int style,string name)
        {
            if((style&0x00c00000)!=0||name=="Progman"||name=="WorkerW"||name=="Shell_TrayWnd"||name=="Shell_SecondaryTrayWnd")return false;
            const int tolerance=2;
            return window.Left<=screen.Left+tolerance&&window.Top<=screen.Top+tolerance&&window.Right>=screen.Right-tolerance&&window.Bottom>=screen.Bottom-tolerance;
        }
        internal static bool IsActive(IntPtr window,IntPtr pet,Rectangle screen)
        {
            if(window==IntPtr.Zero||window==pet||!Native.IsWindowVisible(window)||Native.IsIconic(window))return false;
            Native.Rect r;if(!Native.GetWindowRect(window,out r))return false;
            try{int cloaked;if(Native.DwmInt(window,14,out cloaked,4)==0&&cloaked!=0)return false;Native.Rect frame;if(Native.DwmRect(window,9,out frame,16)==0)r=frame;}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
            StringBuilder cls=new StringBuilder(256);Native.GetClassName(window,cls,cls.Capacity);
            return Covers(Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom),screen,Native.GetWindowLong(window,-16),cls.ToString());
        }
    }

    internal sealed class FullscreenGate
    {
        internal bool Hidden;
        private bool candidate;
        private double since;
        internal bool Update(bool detected,double now)
        {
            if(candidate!=detected){candidate=detected;since=now;}
            if(candidate!=Hidden&&now-since>=(candidate?.15:.25)){Hidden=candidate;return true;}return false;
        }
        internal void Reset(){Hidden=candidate=false;since=0;}
        internal void Set(bool value,double now){Hidden=candidate=value;since=now;}
    }

    internal sealed class Gesture
    {
        internal bool Pressed,Dragged;
        internal Point Origin;
        private Point lastClickPoint;
        private double lastClick=-100;
        internal void Down(Point p){Pressed=true;Dragged=false;Origin=p;}
        internal void Move(Point p){if(Pressed&&(Math.Abs(p.X-Origin.X)>5||Math.Abs(p.Y-Origin.Y)>5))Dragged=true;}
        internal bool Up(Point p,double now)
        {
            if(!Pressed)return false;Pressed=false;
            if(Dragged){lastClick=-100;return false;}
            bool twice=now-lastClick<=SystemInformation.DoubleClickTime/1000.0&&Math.Abs(p.X-lastClickPoint.X)<=SystemInformation.DoubleClickSize.Width&&Math.Abs(p.Y-lastClickPoint.Y)<=SystemInformation.DoubleClickSize.Height;
            lastClick=twice?-100:now;lastClickPoint=p;return twice;
        }
        internal void Cancel(){Pressed=false;Dragged=false;lastClick=-100;}
    }

    internal sealed class Settings
    {
        internal int Scale=50,X,Y;internal bool Position;internal bool Sound=true;internal bool OnTop=true,AutoHide=true;   // v1.8.42：大小默认很小(50)，声音默认开启
        internal int Alpha=100,ReminderMinutes;internal bool Mirror,Chime,AutoRun,Edge,Recycle=true;
        internal int EdgeInset=80;   // v1.8.41f：右贴边头部往屏幕内缩进的像素（锁定为桌面当前值 80，安装包默认即此）
        internal int Hunger=5;   // v1.8.42：初始饥饿条 5 点（0-10，喂一个文件+1，每10分钟-1）
        internal long HungerStamp;   // 下次扣点的 Unix 秒（跨重启保持）
        internal static string Folder{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NaiwaDesktopPet");}}
        internal static Settings Load()
        {
            Settings s=new Settings();try{
                string file=Path.Combine(Folder,"settings.ini");if(!File.Exists(file))return s;
                foreach(string line in File.ReadAllLines(file)){string[] p=line.Split('=');int n;if(p.Length!=2)continue;
                    if(p[0]=="scale"&&int.TryParse(p[1],out n))s.Scale=Math.Max(50,Math.Min(150,n));
                    if(p[0]=="x"&&int.TryParse(p[1],out n)){s.X=n;s.Position=true;}
                    if(p[0]=="y"&&int.TryParse(p[1],out n))s.Y=n;
                    if(p[0]=="sound")s.Sound=p[1]=="True";
                    if(p[0]=="top")s.OnTop=p[1]!="False";
                    if(p[0]=="fullscreen")s.AutoHide=p[1]!="False";
                    if(p[0]=="alpha"&&int.TryParse(p[1],out n))s.Alpha=Math.Max(40,Math.Min(100,n));
                    if(p[0]=="mirror")s.Mirror=p[1]=="True";
                    if(p[0]=="reminder"&&int.TryParse(p[1],out n))s.ReminderMinutes=n;
                    if(p[0]=="chime")s.Chime=p[1]=="True";
                    if(p[0]=="autorun")s.AutoRun=p[1]=="True";
                    if(p[0]=="edge")s.Edge=p[1]=="True";
                    if(p[0]=="recycle")s.Recycle=p[1]!="False";
                    if(p[0]=="edgeInset"&&int.TryParse(p[1],out n))s.EdgeInset=Math.Max(0,Math.Min(160,n));   // v1.8.16
                    if(p[0]=="hunger"&&int.TryParse(p[1],out n))s.Hunger=Math.Max(0,Math.Min(10,n));   // v1.8.37
                    long l;if(p[0]=="hungerStamp"&&long.TryParse(p[1],out l))s.HungerStamp=l;
                }
            }catch{}return s;
        }
        internal void Save(){try{Directory.CreateDirectory(Folder);File.WriteAllLines(Path.Combine(Folder,"settings.ini"),new string[]{"scale="+Scale,"x="+X,"y="+Y,"sound="+Sound,"top="+OnTop,"fullscreen="+AutoHide,"alpha="+Alpha,"mirror="+Mirror,"reminder="+ReminderMinutes,"chime="+Chime,"autorun="+AutoRun,"edge="+Edge,"recycle="+Recycle,"edgeInset="+EdgeInset,"hunger="+Hunger,"hungerStamp="+HungerStamp});}catch{}}
    }

    internal sealed class LayeredSurface:IDisposable
    {
        private IntPtr dc,bmp,old,bits;
        private byte[] buffer;
        private int width,height;
        internal void Update(IntPtr window,Bitmap image,Point location,int alpha)
        {
            if(width!=image.Width||height!=image.Height)Create(image.Width,image.Height);
            BitmapData locked=image.LockBits(new Rectangle(0,0,width,height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
            try{Marshal.Copy(locked.Scan0,buffer,0,buffer.Length);Marshal.Copy(buffer,0,bits,buffer.Length);}finally{image.UnlockBits(locked);}
            Native.Point destination=new Native.Point(location.X,location.Y),origin=new Native.Point(0,0);Native.Size size=new Native.Size(width,height);
            Native.Blend blend=new Native.Blend{Op=0,Flags=0,Alpha=(byte)Math.Max(0,Math.Min(255,alpha)),Format=1};
            if(!Native.UpdateLayeredWindow(window,IntPtr.Zero,ref destination,ref size,dc,ref origin,0,ref blend,2))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        private void Create(int w,int h)
        {
            Dispose();width=w;height=h;buffer=new byte[w*h*4];dc=Native.CreateCompatibleDC(IntPtr.Zero);
            Native.BitmapInfo info=new Native.BitmapInfo{Size=40,Width=w,Height=-h,Planes=1,BitCount=32,Compression=0,SizeImage=(uint)(w*h*4)};
            bmp=Native.CreateDIBSection(dc,ref info,0,out bits,IntPtr.Zero,0);
            if(bmp==IntPtr.Zero)throw new InvalidOperationException("无法创建透明窗口");old=Native.SelectObject(dc,bmp);
        }
        internal bool Hit(int x,int y){return buffer!=null&&x>=0&&y>=0&&x<width&&y<height&&buffer[(y*width+x)*4+3]>24;}
        public void Dispose(){if(dc!=IntPtr.Zero){if(old!=IntPtr.Zero)Native.SelectObject(dc,old);if(bmp!=IntPtr.Zero)Native.DeleteObject(bmp);Native.DeleteDC(dc);}dc=bmp=old=bits=IntPtr.Zero;width=height=0;buffer=null;}
    }

    internal static class Renderer
    {
        internal static Icon CreateIcon(Bitmap image)
        {
            using(Bitmap small=new Bitmap(64,64,PixelFormat.Format32bppArgb))
            {
                using(Graphics g=Graphics.FromImage(small)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(image,new Rectangle(6,0,52,64),new Rectangle(60,40,175,225),GraphicsUnit.Pixel);}
                IntPtr h=small.GetHicon();try{using(Icon borrowed=Icon.FromHandle(h))return (Icon)borrowed.Clone();}finally{Native.DestroyIcon(h);}
            }
        }
        internal const int BubbleHeight=58;
        internal static Bitmap Render(Assets assets,Motion m,int scale)
        {return Render(assets,m,scale,false,100,null,false);}
        // ---- v1.6.1：眼睛回退为原素材 look 帧的左右转眼（跟随鼠标平滑转动，与最初一致）----
        private static Bitmap CompositeEyes(Assets a,bool mirror,double look,double lookV)
        {
            return new Bitmap(a.Eye(mirror?1-look:look));
        }
        // v1.7：rotateRight = 右贴边时奶蛙绕头部中心旋转 30°（只露头）
        internal static Bitmap Render(Assets assets,Motion m,int scale,bool mirror,int alpha,string bubble,bool rotateRight,float danceHq=0)
        {
            // v1.8.14：右贴边隐藏 → 只露出 head.nwp 这颗大头（不再旋转全身），隐藏时无其他动作
            if(rotateRight)
            {
                int hw=assets.Head.Width>0?assets.Head.Width*scale/100:assets.Width*scale/100;
                int hh=assets.Head.Height>0?assets.Head.Height*scale/100:assets.Height*scale/100;
                Bitmap hc=new Bitmap(hw,hh,PixelFormat.Format32bppPArgb);
                using(Graphics g=Graphics.FromImage(hc))
                {
                    g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                    assets.Head.Ensure(assets);if(assets.Head.Ready)Draw(g,assets.Head.Frame(0),new Rectangle(0,0,hw,hh),1);
                }
                return hc;
            }
            int cw=assets.Width;if(m.Sleeping){if(assets.Rest.Ready&&assets.Rest.Width>cw)cw=assets.Rest.Width;if(assets.Rest2.Ready&&assets.Rest2.Width>cw)cw=assets.Rest2.Width;}   // v1.8.40：睡觉时画布加宽容纳笑倒GIF/躺卧定格（唤醒恢复）
            int w=cw*scale/100,ph=assets.Height*scale/100;
            int contentH=ph;
            if(!rotateRight)
            {
                // v1.6.8：内容区高度取最大（素材/drag 350 高/feed），所有奶蛙底部对齐
                if(m.Dragging||m.HoverOpen)contentH=Math.Max(contentH,assets.Drag.Height*scale/100);
                if(m.Feeding)contentH=Math.Max(contentH,assets.Feed.Height*scale/100);
            }
            int h=rotateRight?374*scale/100:contentH+BubbleHeight;
            if(rotateRight)w=433*scale/100;
            Bitmap canvas=new Bitmap(w,h,PixelFormat.Format32bppPArgb);
            using(Graphics g=Graphics.FromImage(canvas))
            {
                g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                // v1.7：右贴边旋转 30°——先平移到旋转后包围盒原点，再绕奶蛙头部中心 (106,60) 旋转
                if(rotateRight)g.TranslateTransform(-76f*scale/100f,45f*scale/100f);
                else g.TranslateTransform(0,BubbleHeight);
                // v1.8：拖动时行走动画朝拖动方向（向左拖 → 镜像朝左）
                // v1.8.8：走路时朝右走镜像，朝左走正面（方向已按观察反转）
                bool drawMirror=mirror;
                if(m.Holding&&m.DragAngle<-.5)drawMirror=!mirror;
                if(m.Walking&&m.WalkDir>0)drawMirror=!mirror;
                if(drawMirror){g.ScaleTransform(-1f,1f);g.TranslateTransform(-w,0);}
                if(rotateRight){g.TranslateTransform(106f*scale/100f,60f*scale/100f);g.RotateTransform(-30f);g.TranslateTransform(-106f*scale/100f,-60f*scale/100f);}
                // Transforms move the supplied artwork; no replacement body or expression is drawn.
                if(m.Moving)
                {
                    float px=w*.5f,py=ph*(m.Holding?.56f:.9565f),s=(float)m.Squash;
                    g.TranslateTransform(px,py-(m.Holding?2f*scale/100:0));g.RotateTransform((float)m.DragAngle);
                    g.ScaleTransform(m.Holding?1.008f:1+s,m.Holding?1.018f:1-s);g.TranslateTransform(-px,-py);
                }
                else if(m.Idling)
                {
                    double t=Math.Max(0,Math.Min(1,(m.Clock-m.IdleStart)/Math.Max(.001,m.IdleEnd-m.IdleStart)));
                    float cx=w*.5f,cy=ph*.5f;g.TranslateTransform(cx,cy);
                    if(m.IdleKind==0)g.RotateTransform((float)(10*Math.Sin(Math.PI*t)));
                    else if(m.IdleKind==1)g.TranslateTransform(0,(float)(-26*Math.Sin(Math.PI*t)));
                    else if(m.IdleKind==2)g.TranslateTransform((float)(14*Math.Sin(2*Math.PI*t)),(float)(-4*Math.Sin(Math.PI*t)));
                    else if(m.IdleKind==3)g.TranslateTransform((float)(3*Math.Sin(30*t)),0);
                    else if(m.IdleKind==4)g.ScaleTransform(1,(float)(1-0.06*Math.Sin(Math.PI*t)));
                    g.TranslateTransform(-cx,-cy);
                }
                else if(m.HeadPoking)
                {
                    double t=Math.Max(0,Math.Min(1,(m.Clock-m.HeadStart)/1.0));
                    float cx=w*.5f,cy=ph*.35f;g.TranslateTransform(cx,cy);g.RotateTransform((float)(4*Math.Sin(Math.PI*t)));
                    g.ScaleTransform(1,(float)(1-0.08*Math.Sin(Math.PI*t)));g.TranslateTransform(-cx,-cy);
                }
                else if(m.Celebrating)
                {
                    double t=m.Clock-m.CelebrateStart;float cx=w*.5f,cy=ph*.5f;
                    g.TranslateTransform(cx,cy);g.TranslateTransform(0,(float)(-26*Math.Abs(Math.Sin(Math.PI*t*2))));
                    g.RotateTransform((float)(2*Math.Sin(Math.PI*t*2)));g.TranslateTransform(-cx,-cy);
                }
                // v1.6.4：拖放素材画布尺寸与素材.nwp 不同（统一裁空白后），居中+底部对齐 contentH
                Rectangle rect,dr;
                if(rotateRight){rect=new Rectangle(0,0,assets.Width*scale/100,assets.Height*scale/100);dr=rect;}
                else
                {
                    rect=new Rectangle(0,contentH-ph,w,ph);
                    dr=rect;
                    if(assets.Drag.Width>0)
                    {
                        float s2=scale/100f;
                        dr=new Rectangle((int)((w-assets.Drag.Width*s2)/2f),(int)(contentH-assets.Drag.Height*s2),(int)(assets.Drag.Width*s2),(int)(assets.Drag.Height*s2));
                    }
                }
                double eyeLook=mirror?1-m.Look:m.Look;
                // v1.8.9：拖起奶蛙 → 显示惊讶表情（hold.nwp）；行走只在落地后触发
                // v1.8.10：按 Hold 素材自身比例渲染（与本体等高 309、等比不变形、背景透明）
                // v1.8.38：重力下落/上抛中也显示惊恐表情（落地自动取消）
                if(m.Holding||m.Falling){assets.Hold.Ensure(assets);if(assets.Hold.Ready){float hs=scale/100f;Rectangle hr=new Rectangle((int)((w-assets.Hold.Width*hs)/2f),(int)(contentH-assets.Hold.Height*hs),(int)(assets.Hold.Width*hs),(int)(assets.Hold.Height*hs));Draw(g,assets.Hold.Frame(0),hr,1);}}
                else if(m.Dragging){assets.Drag.Ensure(assets);if(assets.Drag.Ready)Draw(g,assets.Drag.Frame((int)((m.Clock-m.DragStart)*assets.Drag.Fps)),dr,1);}
                // v1.8.1：行走（落地后/自由散步）——先加载素材再判断 Ready（防止短路）
                else if(m.Walking){assets.Walk.Ensure(assets);if(assets.Walk.Ready){int wf=(int)((m.Clock-m.WalkStart)*12)%assets.Walk.Count;Draw(g,assets.Walk.Frame(wf),dr,1);}}
                // v1.7：拖拽文件悬停时播放一遍"张嘴"段（新素材帧1-19，旧素材帧2-19），播完定格在张开状态
                else if(m.HoverOpen){assets.Drag.Ensure(assets);if(assets.Drag.Ready){int hf=(int)((m.Clock-m.HoverStart)*15);if(hf>=19)hf=18;Draw(g,assets.Drag.Frame(1+hf),dr,1);}}
                // v1.6.4：拖放完成后摆出沮丧表情
                else if(m.Feeding){assets.Feed.Ensure(assets);if(assets.Feed.Ready)Draw(g,assets.Feed.Frame(),dr,1);}
                // v1.8.37：饥饿表情（hunger<3 扣点时触发，跪地祈祷 10 秒）
                else if(m.Hungry){assets.Hunger.Ensure(assets);if(assets.Hunger.Ready){float hs=scale/100f;Rectangle hr=new Rectangle((int)((w-assets.Hunger.Width*hs)/2f),(int)(contentH-assets.Hunger.Height*hs),(int)(assets.Hunger.Width*hs),(int)(assets.Hunger.Height*hs));Draw(g,assets.Hunger.Frame(0),hr,1);}}
                // v1.8.39：喂食后庆祝舞蹈（dance 素材等比放大到本体高度；再次拖文件时张嘴优先）
                else if(m.Dancing){assets.Dance.Ensure(assets);if(assets.Dance.Ready){float dh=assets.Height*scale/100f;float dw=assets.Dance.Width*dh/assets.Dance.Height;Rectangle drn=new Rectangle((int)((w-dw)/2f),(int)(contentH-dh),(int)dw,(int)dh);Draw(g,assets.Dance.Frame((int)((m.Clock-m.DanceStart)*15)%assets.Dance.Count),drn,1);}}
                // v1.8.39：吃饱撑肚——前 1.2 秒本体冒烟，之后变成胖袋鼠
                else if(m.Fat){assets.Fat.Ensure(assets);
                    double ft=m.Clock-m.FatStart;
                    if(ft<1.2)
                    {
                        Draw(g,assets.Frame(0),rect,1);   // 冒烟阶段还是本体
                        for(int i=0;i<3;i++)
                        {
                            double tt=ft*1.0+i*0.4;if(tt>1.2)tt-=1.2;
                            double p=tt/1.2;
                            int px=w/2+(int)((p*24+i*18-27)),py=(int)(rect.Top-18-p*58);
                            int rad=(int)(6+p*15),a=(int)(215*(1-p));
                            using(SolidBrush sb=new SolidBrush(Color.FromArgb(Math.Max(0,a),132,132,132)))g.FillEllipse(sb,px-rad,py-rad,rad*2,rad*2);
                        }
                    }
                    else if(assets.Fat.Ready){float fs=scale/100f;Rectangle fr=new Rectangle((int)((w-assets.Fat.Width*fs)/2f),(int)(contentH-assets.Fat.Height*fs),(int)(assets.Fat.Width*fs),(int)(assets.Fat.Height*fs));Draw(g,assets.Fat.Frame(0),fr,1);}
                }
                // v1.8.17：双击眼睛三阶段动作（1起势/2愤怒/3打电话2秒→爆炸成6只小奶蛙吃东西跳舞）
                else if(m.HeadSequencing){assets.Seq.Ensure(assets);if(m.HeadSeq==3)assets.Dance.Ensure(assets);if(assets.Seq.Ready){float hq=scale/100f;double st=m.Clock-m.HeadSeqStart;if(m.HeadSeq==3&&st>=1)Draw(g,assets.Frame(0),rect,1);   // v1.8.35：炸开后主窗口显示本体，6 只小奶蛙在各独立画布窗口
                else{Rectangle sr=new Rectangle((int)((w-assets.Seq.Width*hq)/2f),(int)(contentH-assets.Seq.Height*hq),(int)(assets.Seq.Width*hq),(int)(assets.Seq.Height*hq));Draw(g,assets.Seq.Frame(m.HeadSeq-1),sr,1);}}}
                else if(m.Resting)Draw(g,assets.Frame(assets.Count-1),rect,1);
                else if(m.Waking){float t=(float)Math.Min(1,(m.Clock-m.WakeStart)/.24);Draw(g,assets.Frame(assets.Count-1),rect,1-t);using(Bitmap eye=CompositeEyes(assets,mirror,m.Look,m.LookV))Draw(g,eye,rect,t);}
                // v1.8.40：睡觉——先播"笑倒"GIF（站立→大笑→倒下），播完定格为躺卧照片，直到鼠标唤醒（必须先 Ensure 加载素材）
                else if(m.Sleeping){assets.Rest.Ensure(assets);assets.Rest2.Ensure(assets);double st=m.Clock-m.SleepStart;if(assets.Rest.Ready&&st<assets.Rest.Count/assets.Rest.Fps){int rf=Math.Min(assets.Rest.Count-1,(int)(st*assets.Rest.Fps));float hs=scale/100f;Rectangle rr=new Rectangle((int)((w-assets.Rest.Width*hs)/2f),(int)(contentH-assets.Rest.Height*hs),(int)(assets.Rest.Width*hs),(int)(assets.Rest.Height*hs));Draw(g,assets.Rest.Frame(rf),rr,1);}else if(assets.Rest2.Ready){float hs=scale/100f;Rectangle rr=new Rectangle((int)((w-assets.Rest2.Width*hs)/2f),(int)(contentH-assets.Rest2.Height*hs),(int)(assets.Rest2.Width*hs),(int)(assets.Rest2.Height*hs));Draw(g,assets.Rest2.Frame(0),rr,1);}else Draw(g,assets.Blink(0,assets.BlinkCount-1),rect,1);}
                else if(!m.Playing)
                {
                    Bitmap idle=m.Blinking?assets.Blink(eyeLook,(int)((m.Clock-m.BlinkStart)*assets.BlinkFps)):CompositeEyes(assets,mirror,m.Look,m.LookV);
                    if(m.Poking)using(Bitmap poke=WarpBelly(idle,m.PokeX,m.PokeY,m.PokeAmount))Draw(g,poke,rect,1);else Draw(g,idle,rect,1);
                }
                else if(m.Elapsed<.10){using(Bitmap eye=CompositeEyes(assets,mirror,m.Look,m.LookV))Draw(g,eye,rect,1);Draw(g,assets.Frame((int)(m.Elapsed*assets.Fps)),rect,(float)(m.Elapsed/.10));}
                else Draw(g,assets.Frame((int)(m.Elapsed*assets.Fps)),rect,1);
                g.ResetTransform();
                if(bubble!=null&&!rotateRight)DrawBubble(g,bubble,w);
            }return canvas;
        }
        private static void DrawBubble(Graphics g,string text,int canvasWidth)
        {
            Font f=null;
            try
            {
                f=new Font("Microsoft YaHei UI",11f);
                SizeF sz=g.MeasureString(text,f);
                while(sz.Width+28>canvasWidth-16&&f.Size>6.5f){f.Dispose();f=new Font("Microsoft YaHei UI",f.Size-0.8f);sz=g.MeasureString(text,f);}   // v1.8.39：长文字自动缩字号，保证气泡完整显示
                float bw=Math.Min(canvasWidth-16,sz.Width+28),bh=40;
                float bx=(canvasWidth-bw)/2,by=6;
                using(GraphicsPath path=RoundedRect(bx,by,bw,bh,10))
                {
                    using(SolidBrush fill=new SolidBrush(Color.FromArgb(216,255,255,255)))g.FillPath(fill,path);
                    using(Pen pen=new Pen(Color.FromArgb(140,90,90,90),1f))g.DrawPath(pen,path);
                }
                PointF[] tri={new PointF(bx+bw/2-7,by+bh),new PointF(bx+bw/2+7,by+bh),new PointF(bx+bw/2,by+bh+11)};
                using(SolidBrush fill=new SolidBrush(Color.FromArgb(216,255,255,255)))g.FillPolygon(fill,tri);
                using(SolidBrush brush=new SolidBrush(Color.FromArgb(235,45,45,45)))g.DrawString(text,f,brush,(canvasWidth-sz.Width)/2,by+(bh-sz.Height)/2);
            }
            finally{if(f!=null)f.Dispose();}
        }
        private static GraphicsPath RoundedRect(float x,float y,float w,float h,float r)
        {
            GraphicsPath p=new GraphicsPath();
            p.AddArc(x,y,r,r,180,90);p.AddArc(x+w-r,y,r,r,270,90);p.AddArc(x+w-r,y+h-r,r,r,0,90);p.AddArc(x,y+h-r,r,r,90,90);p.CloseFigure();return p;
        }
        internal static Bitmap WarpBelly(Bitmap original,double cx,double cy,double amount)
        {
            int w=original.Width,h=original.Height;byte[] source=new byte[w*h*4];
            BitmapData locked=original.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
            try{Marshal.Copy(locked.Scan0,source,0,source.Length);}finally{original.UnlockBits(locked);}
            byte[] result=(byte[])source.Clone();
            const double radiusX=96,radiusY=72;
            int left=Math.Max(0,(int)(cx-radiusX)),right=Math.Min(w-2,(int)(cx+radiusX)),top=Math.Max(105,(int)(cy-radiusY)),bottom=Math.Min(Math.Min(300,h-2),(int)(cy+radiusY));
            for(int y=top;y<=bottom;y++)for(int x=left;x<=right;x++)
            {
                double dx=x-cx,dy=y-cy,r=dx*dx/(radiusX*radiusX)+dy*dy/(radiusY*radiusY);if(r>=1)continue;
                // Include the belly outline, fading out before the face and feet.
                double neck=Math.Min(1,(y-105)/30.0),legs=Math.Min(1,(300-y)/20.0);
                double fade=neck*neck*(3-2*neck)*legs*legs*(3-2*legs);
                double fall=(1-r)*(1-r)*amount*fade;
                double sx=Math.Max(0,Math.Min(w-1.001,x+dx*fall)),sy=Math.Max(0,Math.Min(h-1.001,y+dy*fall*.6));
                int ix=Math.Max(0,Math.Min(w-2,(int)sx)),iy=Math.Max(0,Math.Min(h-2,(int)sy));double fx=sx-ix,fy=sy-iy;
                int dest=(y*w+x)*4,a=(iy*w+ix)*4,b=a+4,c=a+w*4,d=c+4;
                for(int channel=0;channel<4;channel++)result[dest+channel]=(byte)Math.Max(0,Math.Min(255,(source[a+channel]*(1-fx)+source[b+channel]*fx)*(1-fy)+(source[c+channel]*(1-fx)+source[d+channel]*fx)*fy));
            }
            Bitmap warped=new Bitmap(w,h,PixelFormat.Format32bppPArgb);locked=warped.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
            try{Marshal.Copy(result,0,locked.Scan0,result.Length);}finally{warped.UnlockBits(locked);}return warped;
        }
        private static void Draw(Graphics g,Bitmap b,Rectangle rect,float alpha)
        {
            if(alpha<=0)return;
            using(ImageAttributes a=new ImageAttributes())
            {
                a.SetWrapMode(WrapMode.TileFlipXY);if(alpha<1){ColorMatrix c=new ColorMatrix();c.Matrix33=alpha;a.SetColorMatrix(c);}
                g.DrawImage(b,rect,0,0,b.Width,b.Height,GraphicsUnit.Pixel,a);
            }
        }
        // v1.8.17：打电话后出现"很多奶蛙"——本体两侧各 3 只小奶蛙错落簇拥
        // v1.8.18：爆炸散开成 6 只小奶蛙——先点头吃东西，后弹跳舞蹈
        // v1.8.20：改为 3 列 2 行居中排列（画布 213 宽内放得下），从中心爆炸扩散开
        private static void DrawCrowdDance(Graphics g,Assets assets,int canvasW,int contentH,float hs,double t)
        {
            // v1.8.28：原地炸开 → 6 只小奶蛙放大 2 倍，按 3×2 网格各占一格（一只奶蛙一块画布区域）
            //         带重力抛物线下落、触地弹跳衰减 → 落定后在地面播放舞蹈动画（dance.nwp，舞步错开）
            const int N=6;
            Bitmap baseFrame=assets.Frame(0);
            double cx=canvasW/2f;
            double ground=contentH*0.92f;                 // 地面 y
            double spacing=assets.Width*hs*0.60*1.25;     // 格子宽（含空隙，适配 2.2x 画布）
            double rowGap=assets.Height*hs*0.60*1.20;     // 格子高（含空隙）
            assets.Dance.Ensure(assets);
            for(int i=0;i<N;i++)
            {
                int col=i%3,row=i/3;
                double tx=cx+(col-1)*spacing,ty=ground-row*rowGap;   // 网格落点（每格一块）
                double ph=Math.Min(1,t/0.55);          // 0-0.55s：飞行（炸开）
                double ease=1-Math.Pow(1-ph,3);        // 先慢后快：重力加速感
                double x=cx+(tx-cx)*ease+Math.Sin(t*1.6+i)*6;
                double y=contentH*0.30-Math.Sin(ph*Math.PI)*contentH*0.16+(ty-contentH*0.30)*ease;   // 抛物线：先上扬再加速下落
                double b=0;
                if(t>=0.55){double bt=t-0.55;if(bt<1.2)b=Math.Abs(Math.Sin(bt*4.2))*contentH*0.06*Math.Max(0,1-bt*1.2);}   // 触地弹跳衰减
                y-=b;
                double s=0.60;
                Bitmap fr=baseFrame;
                if(t>=1.05&&assets.Dance.Ready)   // 落定后：画布内到处乱跳（抛物线弹跳=有重力）+ 舞蹈动画
                {
                    int fi=((int)((t-1.05)*15)+i*7)%assets.Dance.Count;   // 每只错开 7 帧 → 舞步交错
                    fr=assets.Dance.Frame(fi);
                    s=0.60+0.03*Math.Sin(t*6+i);   // 舞动呼吸缩放
                    double wander=Math.Min(1,(t-1.05)/0.6);   // 0.6s 渐入乱跳
                    double hx=canvasW/2f+(Math.Sin(t*1.7+i*2.1)*0.36+Math.Sin(t*3.1+i*0.8)*0.15)*canvasW;   // 横向到处游走
                    double per=1.35+0.12*(i%3);                 // 跳跃周期（各不相同）
                    double u=((t-1.05+i*0.41)%per)/per;          // 相位
                    double hop=4.0*contentH*0.15*u*(1-u);        // 抛物线弹跳：起跳→上升减速→最高→加速下落→触地（重力感）
                    x=tx+(hx-tx)*wander;
                    y=ground-hop;
                }
                double dw=assets.Width*hs*s,dh=assets.Height*hs*s;
                g.TranslateTransform((float)x,(float)y);
                g.DrawImage(fr,-(float)dw/2f,-(float)dh/2f,(float)dw,(float)dh);
                g.ResetTransform();
            }
        }
    }

    internal sealed class PetWindow:Form
    {
        private readonly Assets assets;
        private readonly bool testing;
        private readonly Settings settings;
        internal readonly Motion Motion=new Motion();
        private readonly Gesture gesture=new Gesture();
        private readonly Stopwatch watch=Stopwatch.StartNew();
        private readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        private readonly LayeredSurface surface=new LayeredSurface();
        private readonly SoundPlayer sound;
        private readonly Icon petIcon;
        private readonly ContextMenuStrip menu;
        // v1.8.39：右键菜单自动关闭——鼠标不在菜单上累计 3 秒自动关闭；点击菜单外区域立即关闭
        private double menuOpenedAt,menuMouseHoverAt;private bool wasMenuLButton;
        // v1.8.39：跳舞中拖文件（已停舞张嘴）→ 本次喂完不再重新跳舞
        private bool danceInterrupted;
        // v1.8.40：喂食跳舞音效（WMP COM 播放 MP3，跳舞结束 1 秒渐隐停止）
        private dynamic danceSound=null;
        private bool danceFading=false;
        private bool crowdSoundStarted=false;   // v1.8.43：舞群小奶蛙跳舞音频（只播一次）
        private double crowdCreatedAt=-1;       // v1.8.43：舞群创建时间（兜底触发音频用）
        private double danceFadeStart=0;
        private static readonly string DanceAudio=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"dance.mp3");
        // v1.8.39：吃饱撑肚——"我的肚子真是肥嘟嘟的"气泡在变袋鼠（冒烟结束）后才显示
        private bool fatBubbleShown;
        private readonly NotifyIcon tray;
        private ToolStripMenuItem fullscreenItem,autoRunItem,hungerItem,soundItem;   // v1.8.37：hungerItem 饥饿条显示项；v1.8.40：soundItem 声音开关
        private Point dragWindow,lastDragPointer;
        private readonly FullscreenGate fullscreenGate=new FullscreenGate();
        private double nextFullscreenCheck;
        private string lastVisual="";
        private bool disposing,menuOpen,ready;
        // v1.8.35：打电话炸开后 6 只小奶蛙各自独立画布窗口
        private List<CrowdWindow> crowdWindows;
        // v1.7.3：重力——拖动松手后奶蛙下落，触底回弹衰减
        private bool gravityFalling;private double gravityVy,lastGravityTick,lastWalkTick;
        // v1.8.30：拖动时的垂直速度（松手时若正快速上移 → 往上抛，再受重力下落）
        private double dragVy,dragVyPeak;   // v1.8.36：dragVyPeak 记录拖动全程最快速上移峰值（松手用它抛，更灵敏）
        // v1.8.37：饥饿条（0-10）——统一存 settings.Hunger（字段与设置同步，Save 才生效）
        internal string LastVisual{get{return lastVisual;}}
        // ---- v1.3 新增：气泡 / 睡眠 / 小动作 / 提醒 / 报时 / 帧率自适应 ----
        private string bubbleText;private double bubbleUntil;
        private double nextWalk=8;   // v1.8.2：待机自由散步计时
        private double lastHover=-1e9,nextIdle=15;
        private double reminderNext=-1;
        private int lastChimeHour;
        private readonly Random extraRandom=new Random();
        internal bool HiddenForFullscreen{get;private set;}
        internal int ScalePercent{get{return settings.Scale;}}
        internal bool SoundEnabled{get{return settings.Sound;}}
        private double Now{get{return watch.Elapsed.TotalSeconds;}}

        internal PetWindow(Assets a,bool test)
        {
            assets=a;testing=test;settings=testing?new Settings():Settings.Load();
            Motion.AutoLook=true;
            // v1.8.37：饥饿条初始化（重启保持；无记录则从现在起 10 分钟后首次扣点）
            if(settings.HungerStamp<=0)settings.HungerStamp=DateTimeOffset.UtcNow.ToUnixTimeSeconds()+600;
            Text=Native.Title;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.StandardClick|ControlStyles.StandardDoubleClick,true);
            ClientSize=new Size(SizeFor(settings.Scale).Width,SizeFor(settings.Scale).Height+Renderer.BubbleHeight);TopMost=settings.OnTop;Cursor=Cursors.Hand;
            // ---- v1.6：支持把文件拖到奶蛙上回收 ----
            AllowDrop=true;
            // v1.8.41e：像素级判定——文件拖到奶蛙可见像素上才张嘴；离开可见像素即沮丧（DragEventArgs 坐标是屏幕坐标，必须 PointToClient 转换后查 surface）
            DragEnter+=delegate(object s,DragEventArgs e){e.Effect=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;if(e.Effect!=DragDropEffects.None&&PixelHit(e))OnHoverEnter();};
            // v1.8.36：行走时窗口在移动，DragEnter 可能被跳过/抖动 → 用 DragOver 持续补张（HoverOpen 已守卫，幂等）
            DragOver+=delegate(object s,DragEventArgs e){e.Effect=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;if(e.Effect!=DragDropEffects.None){if(PixelHit(e)){if(!Motion.HoverOpen)OnHoverEnter();}else OnHoverLeave();}};
            DragLeave+=delegate(object s,EventArgs e){OnHoverLeave();};
            DragDrop+=OnFileDrop;
            if(settings.Position)Location=new Point(settings.X,settings.Y);else Home();
            KeepVisible();
            petIcon=Renderer.CreateIcon(assets.Eye(0));
            Icon=petIcon;
            if(File.Exists(assets.AudioPath)){sound=new SoundPlayer(assets.AudioPath);sound.Load();}
            menu=BuildMenu();
            if(!testing){tray=new NotifyIcon{Icon=petIcon,Text="奶蛙 · 戳肚子 / 双击笑 / 拖动",ContextMenuStrip=menu,Visible=true};tray.DoubleClick+=delegate{ReturnHome();};}
            try{File.AppendAllText(Path.Combine(Settings.Folder,"start.log"),DateTime.Now.ToString("s")+" ctor-ok visible="+Visible+" handle="+IsHandleCreated+Environment.NewLine);}catch{}
            timer.Interval=16;timer.Tick+=delegate{Tick();};
            Shown+=delegate{ready=true;lastChimeHour=DateTime.Now.Hour;if(settings.ReminderMinutes>0)reminderNext=Now+settings.ReminderMinutes*60;RenderNow(true);if(!testing){CheckFullscreen(true);timer.Start();}try{File.AppendAllText(Path.Combine(Settings.Folder,"start.log"),DateTime.Now.ToString("s")+" shown-ok visible="+Visible+" handle="+IsHandleCreated+Environment.NewLine);}catch{}};
        }
        protected override bool ShowWithoutActivation{get{return true;}}
        protected override CreateParams CreateParams{get{CreateParams p=base.CreateParams;p.ExStyle|=0x80000|0x80|0x08000000;p.ClassStyle|=8;return p;}}
        protected override void WndProc(ref Message m)
        {
            if(m.Msg==Native.Wake){ReturnHome();return;}
            if(m.Msg==Native.Status){m.Result=(IntPtr)(HiddenForFullscreen?4:Motion.Resting?3:Motion.Playing?2:Motion.Look>.5?1:0);return;}
            if(m.Msg==Native.Hungry){EatDown();return;}   // v1.8.38：减 1 点饥饿（只减不加，由文件夹 bat 触发）
            if(m.Msg==0x21){m.Result=(IntPtr)3;return;}
            if(m.Msg==0x84 && ready && !gesture.Pressed)
            {
                long value=m.LParam.ToInt64();Point p=PointToClient(new Point((short)(value&0xffff),(short)((value>>16)&0xffff)));
                m.Result=surface.Hit(p.X,p.Y)?(IntPtr)1:new IntPtr(-1);return;
            }
            base.WndProc(ref m);
            if(m.Msg==0x7e && ready){KeepVisible();RenderNow(true);Save();}
        }
        private ContextMenuStrip BuildMenu()
        {
            // v1.8.40：菜单精简为 饥饿条 / 使用说明书 / 设置——大小、查看动作、声音、自启、全屏隐藏、退出全部收进"设置"
            ContextMenuStrip result=new ContextMenuStrip{Font=new Font("Microsoft YaHei UI",10f),ShowImageMargin=false};
            // 饥饿条显示（10 点，喂一个文件 +1，每 10 分钟 -1）
            hungerItem=new ToolStripMenuItem("饥饿：");
            result.Items.Insert(0,hungerItem);
            result.Items.Add("使用说明书",null,delegate{Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"使用说明.txt")){UseShellExecute=true});});
            result.Items.Add(new ToolStripSeparator());
            // ---- 设置（其他所有项收进这里）----
            ToolStripMenuItem settingsMenu=new ToolStripMenuItem("设置");
            // 互动：查看各种动作
            ToolStripMenuItem actions=new ToolStripMenuItem("查看动作");
            string[,] acts={{"大笑","laugh"},{"小动作","idle"},{"行走","walk"},{"起势（第1阶段）","seq1"},{"愤怒（第2阶段）","seq2"},{"打电话·炸开舞群","seq3"},{"喂食庆祝舞蹈","dance"},{"饥饿乞讨","hungry"},{"吃饱变袋鼠","fat"},{"睡觉","sleep"}};   // v1.8.40：去掉"打盹"
            for(int i=0;i<acts.GetLength(0);i++){string label=acts[i,0];string key=acts[i,1];actions.DropDownItems.Add(label,null,delegate{DemoAction(key);});}
            settingsMenu.DropDownItems.Add(actions);
            // 外观：大小
            ToolStripMenuItem sizes=new ToolStripMenuItem("大小");
            int[] values={50,65,80,95,110,125,140,150};string[] labels={"很小(50%)","小号(65%)","中号(80%)","标准(95%)","大号(110%)","加大(125%)","特大(140%)","最大(150%)"};
            for(int i=0;i<values.Length;i++){int v=values[i];ToolStripMenuItem entry=new ToolStripMenuItem(labels[i],null,delegate{SetSize(v);});entry.Tag=v;sizes.DropDownItems.Add(entry);}
            sizes.DropDownItems.Add(new ToolStripSeparator());
            sizes.DropDownItems.Add("放大 5%",null,delegate{SetSize(settings.Scale+5);});
            sizes.DropDownItems.Add("缩小 5%",null,delegate{SetSize(settings.Scale-5);});
            settingsMenu.DropDownItems.Add(sizes);
            // 外观：声音
            soundItem=new ToolStripMenuItem("声音",null,delegate{SetSound(!settings.Sound);});settingsMenu.DropDownItems.Add(soundItem);
            settingsMenu.DropDownItems.Add(new ToolStripSeparator());
            // 系统：自启 / 全屏隐藏
            autoRunItem=new ToolStripMenuItem("开机自启动",null,delegate{SetAutoRun(!settings.AutoRun);});settingsMenu.DropDownItems.Add(autoRunItem);
            fullscreenItem=new ToolStripMenuItem("全屏时自动隐藏",null,delegate{SetAutoHide(!settings.AutoHide);});settingsMenu.DropDownItems.Add(fullscreenItem);
            settingsMenu.DropDownItems.Add(new ToolStripSeparator());
            settingsMenu.DropDownItems.Add("退出奶蛙",null,delegate{Close();});
            result.Items.Add(settingsMenu);
            result.Opening+=delegate{menuOpen=true;UpdateMenu();};
            result.Opened+=delegate{menuOpenedAt=Now;menuMouseHoverAt=Now;};   // v1.8.39：记录打开/停留时刻
            result.Closed+=delegate{menuOpen=false;wasMenuLButton=false;};return result;
        }
        // v1.8.40：查看动作——演示各动作（互斥由各 Motion 方法自身 ClearAll 保证；打电话=连续推进到第3阶段，1秒后炸开舞群）
        private void DemoAction(string name)
        {
            double n=Now;
            switch(name)
            {
                case "laugh":Laugh();return;
                case "idle":Motion.StartIdle(n,extraRandom.Next(4),1400);break;   // v1.8.40：去掉打盹（kind 0-3）
                case "walk":Motion.StartWalk(n,1);break;
                case "seq1":Motion.StartHeadSeq(n);break;
                case "seq2":Motion.StartHeadSeq(n);Motion.StartHeadSeq(n);break;
                case "seq3":Motion.StartHeadSeq(n);Motion.StartHeadSeq(n);Motion.StartHeadSeq(n);break;
                case "dance":Motion.StartDance(n);break;
                case "hungry":Motion.StartHungry(n);bubbleText="我好饿…";bubbleUntil=n+10;break;
                case "fat":Motion.StartFat(n);fatBubbleShown=false;break;
                case "sleep":if(!Motion.Sleep(n))Motion.Sleep(n);break;
            }
            RenderNow(true);
        }
        private void UpdateMenu()
        {
            if(menu==null)return;
            SetToggleMark(autoRunItem,settings.AutoRun);SetToggleMark(fullscreenItem,settings.AutoHide);
            if(soundItem!=null)SetToggleMark(soundItem,settings.Sound&&sound!=null);   // v1.8.40：声音开关勾选标记
            if(hungerItem!=null)   // v1.8.37：饥饿条实时刷新（▓×饥饿数 + ░×空数）
            {
                string bar="";
                for(int i=0;i<10;i++)bar+=i<settings.Hunger?"▓":"░";
                hungerItem.Text="饥饿 "+bar+" "+settings.Hunger+"/10";
                hungerItem.Enabled=false;   // 纯显示项，不可点击
            }
            foreach(ToolStripItem entry in menu.Items){ToolStripMenuItem p=entry as ToolStripMenuItem;if(p==null)continue;foreach(ToolStripItem child in p.DropDownItems){ToolStripMenuItem c=child as ToolStripMenuItem;if(c!=null&&c.Tag is int)c.Checked=(int)c.Tag==settings.Scale;}}
        }
        private static void SetToggleMark(ToolStripMenuItem item,bool selected)
        {
            item.Checked=selected;
            // Use the native right-aligned column for status; no shortcut key is assigned.
            item.ShowShortcutKeys=true;item.ShortcutKeyDisplayString=selected?"✓":" ";
        }
        // v1.8.40：鼠标是否在菜单树（含展开的二级/三级子菜单）上
        private bool MouseOnMenuTree()
        {
            if(menu.Bounds.Contains(Cursor.Position))return true;
            foreach(ToolStripItem it in menu.Items)
            {
                ToolStripMenuItem p=it as ToolStripMenuItem;if(p==null)continue;
                if(p.DropDown.Visible&&p.DropDown.Bounds.Contains(Cursor.Position))return true;
                foreach(ToolStripItem c in p.DropDownItems)
                {
                    ToolStripMenuItem cc=c as ToolStripMenuItem;
                    if(cc!=null&&cc.DropDown.Visible&&cc.DropDown.Bounds.Contains(Cursor.Position))return true;
                }
            }
            return false;
        }
        internal void SetSound(bool enabled)
        {
            settings.Sound=enabled&&sound!=null;
            if(!settings.Sound&&sound!=null)sound.Stop();
            // Enabling sound takes effect at the next laugh, so audio never starts out of sync.
            UpdateMenu();Save();
        }
        // v1.8.40：喂食跳舞音效——跳舞开始播放 dance.mp3，舞蹈结束 1 秒渐隐停止
        internal static void LogAudio(string msg){try{File.AppendAllText(Path.Combine(Settings.Folder,"audio.log"),DateTime.Now.ToString("s")+" "+msg+Environment.NewLine);}catch{}}   // v1.8.43：音频诊断日志
        private void StartDanceSound()
        {
            try
            {
                if(!File.Exists(DanceAudio)){LogAudio("StartDanceSound: 无 dance.mp3");return;}
                if(!settings.Sound){LogAudio("StartDanceSound: 声音开关关");return;}
                if(danceSound==null)
                {
                    Type t=Type.GetTypeFromProgID("WMPlayer.OCX");
                    if(t==null){LogAudio("StartDanceSound: WMPlayer.OCX 未注册");return;}
                    danceSound=Activator.CreateInstance(t);
                }
                danceSound.uiMode="none";
                danceSound.settings.volume=100;
                danceSound.URL=DanceAudio;
                danceSound.controls.play();
                danceFading=false;
                LogAudio("StartDanceSound: 已开始播放");
            }catch(Exception ex){LogAudio("StartDanceSound 异常: "+ex.Message);}
        }
        // v1.8.43：舞群小奶蛙跳舞音频——0 号小蛙落地开始跳舞时由 owner 触发，复用 dance.mp3
        internal void StartCrowdSound()
        {
            LogAudio("StartCrowdSound 被调用, crowdSoundStarted="+crowdSoundStarted);   // v1.8.43：最先记录，确认调用链
            if(crowdSoundStarted)return;
            crowdSoundStarted=true;
            LogAudio("StartCrowdSound: 触发 settings.Sound="+settings.Sound);
            if(!settings.Sound)return;
            StartDanceSound();
        }
        private void UpdateDanceSound()
        {
            try
            {
                if(crowdWindows!=null)
                {
                    // v1.8.43：舞群期间本体音效由舞群接管；兜底——炸开后 0.1 秒强制补触发音频
                    if(!crowdSoundStarted&&crowdCreatedAt>=0&&Now-crowdCreatedAt>=0.1)StartCrowdSound();
                    return;
                }
                bool enabled=settings.Sound&&File.Exists(DanceAudio);   // v1.8.40：受"声音"开关控制
                if(enabled&&Motion.Dancing)
                {
                    // v1.8.43：音效延迟 1 秒再播，与跳舞动作（吃完后的舞姿）对齐
                    if(Motion.Clock-Motion.DanceStart>=1.0&&(danceSound==null||danceFading))StartDanceSound();
                }
                else
                {
                    if(danceSound!=null&&!danceFading){danceFading=true;danceFadeStart=Now;}   // 舞蹈结束/声音关闭 → 开始 1 秒渐隐
                    if(danceSound!=null&&danceFading)
                    {
                        double t=Now-danceFadeStart;
                        if(t>=1){danceSound.controls.stop();danceSound.URL="";danceFading=false;}
                        else danceSound.settings.volume=Math.Max(0,100-(int)(100*t));
                    }
                }
            }catch{}
        }
        internal bool Laugh()
        {            if(HiddenForFullscreen)return false;
            if(Motion.Playing)return false;   // v1.8.41d：互斥先判，避免先出声再取消
            if(sound!=null&&settings.Sound&&!testing)sound.Play();   // v1.8.41d：触发大笑瞬间立即出声（先于动画状态与渲染，消除滞后感）
            Motion.Laugh(Now);   // 已判 Playing，必然成功
            RenderNow(true);return true;
        }
        internal void StopLaugh(){Motion.Stop();if(sound!=null)sound.Stop();RenderNow(true);}
        internal void Tick(){TickNow();}
        internal void TickNow()
        { TickWithForeground(Native.GetForegroundWindow()); }
        internal void TickWithForeground(IntPtr foreground)
        {
            if(Now>=nextFullscreenCheck){nextFullscreenCheck=Now+.12;CheckFullscreenWindow(foreground,false);}
            // v1.8.39：右键菜单——鼠标不在菜单上累计 3 秒自动关闭；点击菜单外区域立即关闭
            if(menuOpen&&menu.Visible)
            {
                // v1.8.40：鼠标在菜单树（含展开的"设置/查看动作"子菜单）上 → 视为停留刷新计时；离开菜单树累计 3 秒未回 → 自动关闭
                if(MouseOnMenuTree()){menuMouseHoverAt=Now;wasMenuLButton=false;}
                else
                {
                    if(Now-menuMouseHoverAt>=3)menu.Close();
                    else
                    {
                        bool lb=(Native.GetAsyncKeyState(0x01)&0x8000)!=0;   // 左键从抬起→按下且不在菜单上 → 关闭
                        if(lb&&!wasMenuLButton)menu.Close();
                        wasMenuLButton=lb;
                    }
                }
            }
            if(HiddenForFullscreen)return;
            Point p=PointToClient(Cursor.Position);bool over=!menuOpen&&surface.Hit(p.X,p.Y);
            // ---- v1.4：眼睛始终跟随鼠标（按相对角度平滑转动，无需鼠标停在身上）----
            if(Motion.AutoLook)
            {
                double cx=Left+Width/2.0,cy=Top+PetHeight/2.0,dx=Cursor.Position.X-cx,dy=Cursor.Position.Y-cy;
                double dist=Math.Max(40,Math.Sqrt(dx*dx+dy*dy));
                Motion.LookGoal=Math.Max(0,Math.Min(1,.5+dx/(2*Math.Max(180,dist))));
                // v1.5：垂直分量（鼠标在上方 → 向上看）
                Motion.LookGoalV=Math.Max(0,Math.Min(1,.5-dy/(2*Math.Max(180,dist))));
            }
            // ---- v1.3：气泡过期 / 打盹 / 随机小动作 ----
            if(bubbleText!=null&&Now>=bubbleUntil){bubbleText=null;lastVisual="";}
            if(over){lastHover=Now;if(Motion.Sleeping)Motion.Wake(Now);if(Motion.Walking)Motion.WalkStart=-1;}   // v1.8.12：行走时鼠标在身上 → 停止行走
            else
            {
                // v1.8.14：停稳直接走已内置于 UpdateGravity（触底静止瞬间 StartWalk）
                // v1.8.16：右贴边隐藏时禁止一切自动动作（不睡觉/不散步/不走路）
                if(!edgeRight&&!Motion.Sleeping&&!Motion.HasExtra&&!Motion.Playing&&!Motion.Resting&&!Motion.Dragging&&!Motion.HoverOpen&&!Motion.Feeding&&!gesture.Pressed&&!menuOpen&&Now-lastHover>120)Motion.Sleep(Now);
                else if(!edgeRight&&!Motion.Sleeping&&!Motion.HasExtra&&!Motion.Playing&&!Motion.Resting&&!Motion.Dragging&&!Motion.HoverOpen&&!Motion.Feeding&&!gesture.Pressed&&!menuOpen&&Now>=nextIdle)
                {
                    int kind=extraRandom.Next(4);   // v1.8.40：去掉"打盹"（原 kind==2）
                    Motion.StartIdle(Now,kind,kind==0?1100:kind==3?700:900);
                    nextIdle=Now+14+extraRandom.NextDouble()*16;
                }
                // v1.8.2：待机自由散步——没在睡/没动画时，奶蛙自己随机走一段
                else if(!edgeRight&&!Motion.Sleeping&&!Motion.HasExtra&&!Motion.Playing&&!Motion.Resting&&!Motion.Dragging&&!Motion.HoverOpen&&!Motion.Feeding&&!gesture.Pressed&&!menuOpen&&!gravityFalling&&Now>=nextWalk)   // v1.8.13：下落中禁止散步；v1.8.16：贴边隐藏禁止
                {
                    Motion.StartWalk(Now,extraRandom.Next(2)*2-1);
                    nextWalk=Now+30+extraRandom.NextDouble()*40;
                }
            }
            bool wasPlaying=Motion.Playing;Motion.Update(Now,over,assets.Duration,assets.BlinkCount/assets.BlinkFps);
            if(wasPlaying&&!Motion.Playing&&sound!=null)sound.Stop();
            UpdateDanceSound();   // v1.8.40：喂食跳舞音效——跳舞播放 / 结束 1 秒渐隐停止
            // v1.8.39：吃饱撑肚——变袋鼠（冒烟1.2秒结束）后才显示"我的肚子真是肥嘟嘟的"
            if(Motion.Fat&&!fatBubbleShown&&Motion.Clock-Motion.FatStart>=1.2){fatBubbleShown=true;ShowBubble("我的肚子真是肥嘟嘟的",5);}
            // ---- v1.8.37：饥饿条——每 10 分钟扣 1 点；hunger<3 时每次扣点触发饥饿表情 10 秒 ----
            long nowSec=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if(settings.Hunger>0&&nowSec>=settings.HungerStamp)
            {
                settings.Hunger--;settings.HungerStamp=nowSec+600;
                if(settings.Hunger<3){Motion.StartHungry(Now);bubbleText="我好饿…";bubbleUntil=Now+10;}
                Save();UpdateMenu();
            }
            // ---- v1.8.35：打电话 1 秒后炸开 → 6 只小奶蛙各自独立画布窗口（不再放大主窗口）----
            // v1.8.36：炸开期间本体隐藏，跳完舞才重新出现
            bool crowdNow=Motion.HeadSequencing&&Motion.HeadSeq==3&&Motion.Clock-Motion.HeadSeqStart>=1;
            if(crowdNow&&crowdWindows==null)
            {
                Point center=PointToScreen(new Point(Width/2,Renderer.BubbleHeight+PetHeight/2));
                Rectangle area=Screen.FromPoint(center).WorkingArea;
                crowdWindows=new List<CrowdWindow>();
                crowdSoundStarted=false;   // v1.8.43：舞群开始，允许跳舞音频播放
                crowdCreatedAt=Now;        // v1.8.43：兜底触发时间基准
                for(int i=0;i<6;i++)crowdWindows.Add(new CrowdWindow(assets,i,center,area.Bottom,settings.Scale,this));
                Visible=false;   // 本体隐藏，等小奶蛙跳完舞再出现
            }
            if(!crowdNow&&crowdWindows!=null)
            {
                foreach(CrowdWindow c in crowdWindows)c.Close();
                crowdWindows=null;
                if(danceSound!=null){try{danceSound.controls.stop();danceSound.URL="";danceFading=false;}catch{}}   // v1.8.43：舞群结束停跳舞音频
                Visible=true;RenderNow(true);   // 本体回归
            }
            // ---- v1.7.3：重力——拖动松手后下落，触底回弹衰减 ----
            UpdateGravity();
            // ---- v1.8.1：重力落地后的行走（横向移动，碰边转向）----
            UpdateWalkMove();
            // v1.8.40：睡觉时窗口宽度扩展显示笑倒GIF/躺卧定格（唤醒自动恢复；右贴边隐藏保持原宽）
            if(!edgeRight){int wantW=assets.Width*settings.Scale/100;if(Motion.Sleeping){if(assets.Rest.Ready&&assets.Rest.Width>assets.Width)wantW=assets.Rest.Width*settings.Scale/100;if(assets.Rest2.Ready&&assets.Rest2.Width>assets.Width)wantW=assets.Rest2.Width*settings.Scale/100;}if(Width!=wantW){int cx=Left+Width/2;Width=wantW;Location=new Point(cx-Width/2,Location.Y);lastVisual="";}}
            // ---- v1.6：回收动画播完自动结束，进入沮丧表情（v1.6.4）----
            FinishDragIfDone();
            // ---- v1.3：休息提醒 / 整点报时 ----
            if(settings.ReminderMinutes>0&&reminderNext>=0&&Now>=reminderNext)
            {
                reminderNext=Now+settings.ReminderMinutes*60;
                if(Motion.Sleeping)Motion.Wake(Now);
                Motion.Celebrate(Now);ShowBubble("该休息啦，起来活动一下！",8);
                if(!(sound!=null&&settings.Sound)){try{SystemSounds.Asterisk.Play();}catch{}}
            }
            if(settings.Chime)
            {
                int h=DateTime.Now.Hour;
                if(h!=lastChimeHour&&DateTime.Now.Minute==0&&DateTime.Now.Second<4){lastChimeHour=h;Motion.Celebrate(Now);ShowBubble("现在 "+DateTime.Now.ToString("HH:mm")+" 啦",5);}
            }
            // ---- v1.3：帧率自适应（空闲降频省 CPU，睡眠更低）----
            int target=HiddenForFullscreen?150:(Motion.HoverOpen||Motion.Playing||Motion.Holding||Motion.Poking||Motion.HasExtra||gravityFalling||bubbleText!=null||danceFading?16:(Motion.Sleeping&&assets.Rest.Ready&&Motion.Clock-Motion.SleepStart<assets.Rest.Count/assets.Rest.Fps?16:(Motion.Sleeping?300:100)));   // v1.8.40：音效渐隐期间保持流畅帧率
            if(timer.Interval!=target)timer.Interval=target;
            RenderNow(false);
        }
        internal void FinishDragIfDone()
        {
            if(Motion.Dragging&&Motion.Clock-Motion.DragStart>=Motion.DragDuration)Motion.DragStart=-1;
        }
        // ---- v1.8.38：文件夹"饿一点.bat" → 手动减 1 点饥饿（只减不加，不经过 10 分钟计时）----
        private void EatDown()
        {
            if(settings.Hunger>0)settings.Hunger--;
            if(settings.Hunger<3&&!Motion.Hungry){Motion.StartHungry(Now);bubbleText="我好饿…";bubbleUntil=Now+10;}
            Save();UpdateMenu();RenderNow(true);
        }
        // ---- v1.8.14：停稳直接走——落地弹跳衰减静止瞬间即触发行走（无延迟）----
        private void UpdateGravity()
        {
            if(!gravityFalling)return;
            double dt=Math.Min(.1,Math.Max(0,Now-lastGravityTick));lastGravityTick=Now;
            gravityVy+=1500*dt;
            Rectangle area=Screen.FromPoint(Cursor.Position).WorkingArea;
            double bottom=area.Bottom-Height;
            double ny=Location.Y+gravityVy*dt;
            if(ny<=area.Top){ny=area.Top;gravityVy=Math.Abs(gravityVy)*.35;}   // v1.8.36：上抛碰屏幕顶 → 回弹（不会飞出屏幕）
            if(ny>=bottom)
            {
                ny=bottom;gravityVy*=-.45;
                if(Math.Abs(gravityVy)<50){gravityFalling=false;Motion.Falling=false;Motion.LandingStart=Now;Motion.Start=-1;Motion.StartWalk(Now,extraRandom.Next(2)*2-1);}   // 停稳瞬间直接行走（v1.8.38：清惊恐表情）
                else Motion.LandingStart=Now;   // 每次回弹触发落地轻弹动画
            }
            Location=new Point(Location.X,(int)Math.Round(ny));
            lastVisual="";                        // 位置变化强制重渲染
        }
        // ---- v1.8.1：行走横向移动（碰屏边缘转向）----
        private void UpdateWalkMove()
        {
            if(!Motion.Walking||gravityFalling||edgeRight)return;   // v1.8.13：下落中禁止横移；v1.8.16：贴边隐藏禁止横移
            double dt=Math.Min(.1,Math.Max(0,Now-lastWalkTick));lastWalkTick=Now;
            int nx=Location.X+(int)Math.Round(120*dt*Motion.WalkDir);
            Rectangle area=Screen.FromPoint(Cursor.Position).WorkingArea;
            if(nx<=area.Left){nx=area.Left;Motion.WalkDir=1;}
            else if(nx+Width>=area.Right){nx=area.Right-Width;Motion.WalkDir=-1;}
            Location=new Point(nx,Location.Y);
            lastVisual="";
        }
        private void ShowBubble(string text,double seconds){bubbleText=text;bubbleUntil=Now+seconds;RenderNow(true);}
        // ---- v1.6：拖入文件 → 播放回收动画 + 移入回收站（可撤销）----
        internal void SimulateHoverEnter(){OnHoverEnter();}
        internal void SimulateHoverLeave(){OnHoverLeave();}
        // v1.8.41e：拖放像素判定——DragEventArgs 的 X/Y 是屏幕坐标，PointToClient 转窗口客户端坐标
        // v1.8.43：张嘴触发像素点 = 沮丧图片（Feed 首帧）的非透明像素掩膜；文件进入沮丧表情显示区域即张嘴
        private bool PixelHit(DragEventArgs e){Point c=PointToClient(new Point(e.X,e.Y));return SadHit(c.X,c.Y);}
        private byte[] sadMask;private int sadW,sadH;   // 沮丧掩膜：Feed 首帧 alpha
        private void EnsureSadMask()
        {
            if(sadMask!=null)return;
            try
            {
                assets.Feed.Ensure(assets);
                if(!assets.Feed.Ready)return;
                Bitmap f=assets.Feed.Frame();
                if(f==null)return;
                sadW=f.Width;sadH=f.Height;
                sadMask=new byte[sadW*sadH];
                System.Drawing.Imaging.BitmapData bd=f.LockBits(new Rectangle(0,0,sadW,sadH),System.Drawing.Imaging.ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                try
                {
                    byte[] raw=new byte[sadW*sadH*4];
                    System.Runtime.InteropServices.Marshal.Copy(bd.Scan0,raw,0,raw.Length);
                    for(int i=0;i<sadW*sadH;i++)sadMask[i]=raw[i*4+3];
                }
                finally{f.UnlockBits(bd);}
            }catch{}
        }
        private bool SadHit(int x,int y)
        {
            EnsureSadMask();
            if(sadMask==null)return false;
            int dw=assets.Width*settings.Scale/100,dh=assets.Height*settings.Scale/100;
            if(dw<=0||dh<=0)return false;
            int rx=(Width-dw)/2,ry=Height-dh;   // 奶蛙渲染矩形：窗口底部居中
            int sx=(x-rx)*sadW/dw,sy=(y-ry)*sadH/dh;   // 窗口坐标 → 沮丧图素材坐标
            if(sx<0||sy<0||sx>=sadW||sy>=sadH)return false;
            return sadMask[sy*sadW+sx]>24;
        }
        private void OnHoverEnter()
        {
            if(edgeRight)return;   // v1.8.14：右贴边隐藏时拖文件过来不张嘴不回收（只允许拖出）
            if(Motion.HoverOpen)return;   // 已在张嘴
            if(Motion.Dragging)Motion.DragStart=-1;   // v1.8.39：回收动画播放中再拖文件 → 先停掉当前回收动画再张嘴
            if(Motion.Walking)Motion.WalkStart=-1;   // v1.8.12：行走中拖文件过来 → 停止行走再张嘴
            if(Motion.Dancing){Motion.EndDance();danceInterrupted=true;}   // v1.8.39：跳舞中拖文件过来 → 停止跳舞再张嘴（喂完不再重新跳）
            if(Motion.Sleeping)Motion.Wake(Now);
            Motion.HoverOpen=true;Motion.HoverStart=Now;Motion.FeedStart=-1;RenderNow(true);
        }
        private void OnHoverLeave()
        {
            if(!Motion.HoverOpen)return;
            Motion.HoverOpen=false;
            danceInterrupted=false;   // 拖走没放下 → 恢复普通喂食规则（下次喂完照常跳舞）
            // v1.8.41d：沮丧——拖走即摆（去掉时间防抖，用户快速拖走也要看到）；行走/下落中不摆（窗口移动导致的误触发仍防住）
            if(!Motion.Dragging&&!Motion.Walking&&!gravityFalling){Motion.ClearEffects(Now);Motion.FeedStart=Now;}
            RenderNow(true);
        }
        internal void OnFileDrop(object sender,DragEventArgs e)
        {
            if(!e.Data.GetDataPresent(DataFormats.FileDrop))return;
            if(edgeRight)return;   // v1.8.14：右贴边隐藏时拖放文件不回收（只允许拖出）
            string[] files=(string[])e.Data.GetData(DataFormats.FileDrop);
            if(files==null||files.Length==0)return;
            if(Motion.Sleeping)Motion.Wake(Now);
            Motion.StartDrag(Now,assets.Drag!=null?assets.Drag.Duration:6);
            int moved=settings.Recycle?RecycleFiles(files):0;
            if(moved>0)   // v1.8.37：喂一个文件 +1 饥饿点（最多 10），并清除饥饿表情
            {
                bool wasStarving=settings.Hunger<3;   // v1.8.39：喂前处于饥饿（<3）
                if(settings.Hunger<10)settings.Hunger++;
                settings.HungerStamp=DateTimeOffset.UtcNow.ToUnixTimeSeconds()+600;   // 喂饱后从新计时
                if(Motion.Hungry)Motion.EndHungry();
                if(settings.Hunger>=10)   // v1.8.39：饥饿满 10 点 → 回收动画只播张嘴+收嘴段（帧0~21，约1.4秒），直接冒烟变胖袋鼠
                {
                    Motion.DragDuration=Math.Min(Motion.DragDuration,1.4);   // 帧0~19张嘴 + 帧20~21收嘴，跳过后面的迈步/晃动/跳舞段
                    Motion.StartFat(Now);
                    fatBubbleShown=false;   // 气泡等变袋鼠后再显示
                }
                else
                {
                    if(!danceInterrupted)Motion.StartDance(Now);   // 普通喂食喂完跳舞；跳舞中喂（已停舞张嘴）→ 喂完不再重新跳
                    ShowBubble("谢谢你！！",4);
                }
                danceInterrupted=false;
                Save();UpdateMenu();
            }
            else if(settings.Recycle)ShowBubble("回收失败（文件被占用？）已保留",4);
            else ShowBubble("收到 "+files.Length+" 个文件（回收已关闭）",3);
        }
        // ---- v1.8.39：舞群（6 只小奶蛙跳舞）期间拖文件到小蛙上 → 取消跳舞、本体回归、张嘴回收 ----
        internal void FeedFromCrowd(DragEventArgs e)
        {
            if(crowdWindows!=null){foreach(CrowdWindow c in crowdWindows)c.Close();crowdWindows=null;}
            Motion.EndHeadSeq();
            Visible=true;
            OnHoverEnter();   // 触发张嘴
            OnFileDrop(this,e);   // 回收文件 + hunger++（张嘴动画与回收动画衔接：drag 开头即张嘴段）
            RenderNow(true);
        }
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;public uint wFunc;public string pFrom;public IntPtr pTo;
            public ushort fFlags;public int fAnyOperationsAborted;public IntPtr hNameMappings;public string lpszProgressTitle;
        }
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT op);
        private static int RecycleFiles(string[] files)
        {
            try
            {
                StringBuilder sb=new StringBuilder();
                foreach(string f in files){sb.Append(f);sb.Append('\0');}
                sb.Append('\0');
                SHFILEOPSTRUCT op=new SHFILEOPSTRUCT{wFunc=3,pFrom=sb.ToString(),fFlags=0x40|0x10|0x200};
                return SHFileOperation(ref op)==0?files.Length:0;
            }
            catch{return 0;}
        }
        internal void RenderNow(bool force)
        {
            if(!IsHandleCreated||disposing||HiddenForFullscreen)return;
            if(crowdWindows!=null)return;   // v1.8.36：本体隐藏期间主窗口不渲染（6 只独立窗口跳舞）
            // v1.8.40：睡觉——key 带动画帧号（否则渲染缓存判定画面不变，笑倒 GIF 不播放）
            string sleepKey="0";
            if(Motion.Sleeping&&assets.Rest.Ready){double st=Motion.Clock-Motion.SleepStart;sleepKey=st<assets.Rest.Count/assets.Rest.Fps?"a"+Math.Min(assets.Rest.Count-1,(int)(st*assets.Rest.Fps)):"z";}
            string key=Motion.Dragging?"D"+(int)((Motion.Clock-Motion.DragStart)*assets.Drag.Fps):Motion.HoverOpen?"H"+Math.Min(18,(int)((Motion.Clock-Motion.HoverStart)*15)):Motion.Feeding?"F":Motion.Resting?"R":Motion.Playing?"L"+(int)(Motion.Elapsed*assets.Fps):Motion.Sleeping?"S"+sleepKey:Motion.Blinking?"B"+(Motion.Look>.5?1:0)+"/"+(int)((Motion.Clock-Motion.BlinkStart)*assets.BlinkFps):"E"+(int)Math.Round(Motion.Look*(assets.EyeCount-1))+"/V"+(int)Math.Round(Motion.LookV*30);
            // v1.7.3：Landing（落地轻弹）纳入 key，落地回弹动画逐帧播放；v1.8.1：行走同理
            if(Motion.Poking||Motion.Moving||Motion.Waking||Motion.Idling||Motion.HeadPoking||Motion.Celebrating||Motion.Landing||Motion.Walking||Motion.Falling)key+="/M"+(int)(Motion.Clock*60);   // v1.8.38：下落中逐帧重绘惊恐表情
            key+="/"+settings.Scale+"/"+settings.Alpha+(settings.Mirror?"/F":"");
            if(!force&&key==lastVisual)return;
            string bubble=(bubbleText!=null&&Now<bubbleUntil)?bubbleText:(Motion.Sleeping?"z Z Z":null);
            using(Bitmap frame=Renderer.Render(assets,Motion,settings.Scale,settings.Mirror,settings.Alpha,bubble,edgeRight))surface.Update(Handle,frame,Location,settings.Alpha*255/100);
            lastVisual=key;
        }
        internal void SetAutoHide(bool value)
        {
            settings.AutoHide=value;fullscreenGate.Reset();
            if(!value)ApplyFullscreen(false);else if(!testing)CheckFullscreen(true);
            Save();UpdateMenu();
        }
        // ---- v1.3 新增设置入口 ----
        internal void SetAlpha(int value){settings.Alpha=Math.Max(40,Math.Min(100,value));RenderNow(true);Save();UpdateMenu();}
        internal void SetMirror(bool value){settings.Mirror=value;RenderNow(true);Save();UpdateMenu();}
        internal void SetReminder(int minutes)
        {
            settings.ReminderMinutes=Math.Max(0,Math.Min(240,minutes));
            reminderNext=settings.ReminderMinutes>0?Now+settings.ReminderMinutes*60:-1;
            Save();UpdateMenu();
        }
        internal void SetChime(bool value){settings.Chime=value;Save();UpdateMenu();}
        internal void SetAutoRun(bool value)
        {
            settings.AutoRun=value;
            try
            {
                using(RegistryKey key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true))
                {
                    if(key==null)return;
                    if(value)key.SetValue("NaiwaDesktopPet","\""+Application.ExecutablePath+"\"");
                    else key.DeleteValue("NaiwaDesktopPet",false);
                }
            }catch{}
            Save();UpdateMenu();
        }
        // ---- v1.4：贴边停靠（只露头，身体藏到屏幕外）----
        private bool edgeRight;   // v1.7：拖到屏幕右侧自动贴边（只露头）
        internal void SetEdge(bool value)
        {
            settings.Edge=value;edgeRight=false;
            if(value)EdgeDock();else Home();
            Save();UpdateMenu();RenderNow(true);
        }
        // v1.8.41f：隐藏露出锁定——露出大小固定为 50% 基准（即桌面当前效果），不再随"大小"设置缩放；本体缩放不影响隐藏露出的头
        private int EdgeHeadPx(){return (int)(118*50/100.0)+Renderer.BubbleHeight;}
        private int EdgeHeadWidth(){return assets.Head.Width>0?assets.Head.Width*50/100:(int)(54*50/100.0);}
        private void EdgeDock()
        {
            Rectangle area=Screen.FromPoint(Cursor.Position).WorkingArea;
            Location=new Point(Math.Max(area.Left,Math.Min(Left,area.Right-Width)),area.Bottom-EdgeHeadPx());
            KeepVisible();
        }
        // v1.7：右贴边——窗口藏到屏幕右缘外，只露出左侧头部（奶蛙旋转 30°）
        private void SetEdgeRight(bool value)
        {
            edgeRight=value;
            if(value)
            {
                ClientSize=new Size(EdgeHeadWidth(),assets.Head.Height>0?assets.Head.Height*50/100:374*50/100);   // v1.8.41f：露出锁定（50% 基准，不随大小设置缩放）
                Rectangle area=Screen.FromPoint(Cursor.Position).WorkingArea;
                Location=new Point(area.Right-EdgeHeadWidth()+settings.EdgeInset,Math.Max(area.Top,Math.Min(Top,area.Bottom-Height)));   // v1.8.16：缩进可调
            }
            else ClientSize=new Size(SizeFor(settings.Scale).Width,SizeFor(settings.Scale).Height+Renderer.BubbleHeight);
            RenderNow(true);
        }
        private void EdgeDockRight()
        {
            Rectangle area=Screen.FromPoint(Cursor.Position).WorkingArea;
            Location=new Point(area.Right-EdgeHeadWidth()+settings.EdgeInset,Math.Max(area.Top,Math.Min(Location.Y,area.Bottom-PetHeight)));   // v1.8.16
            KeepVisible();
        }
        private void CheckFullscreen(bool immediate)
        {CheckFullscreenWindow(Native.GetForegroundWindow(),immediate);}
        private void CheckFullscreenWindow(IntPtr foreground,bool immediate)
        {
            if(!ready||menuOpen||gesture.Pressed)return;
            bool detected=settings.AutoHide&&Fullscreen.IsActive(foreground,Handle,Screen.FromRectangle(Bounds).Bounds);
            if(immediate){fullscreenGate.Set(detected,Now);ApplyFullscreen(detected);}
            else if(fullscreenGate.Update(detected,Now))ApplyFullscreen(fullscreenGate.Hidden);
        }
        internal void ApplyFullscreen(bool hidden)
        {
            if(hidden==HiddenForFullscreen)return;
            HiddenForFullscreen=hidden;
            if(hidden)
            {
                gesture.Cancel();Capture=false;Motion.Suspend(Now);if(sound!=null)sound.Stop();
                Hide();timer.Interval=150;
            }
            else
            {
                Motion.Suspend(Now);timer.Interval=16;Show();RenderNow(true);
            }
        }
        private void ReturnHome(){if(edgeRight)SetEdgeRight(false);if(settings.Edge){settings.Edge=false;UpdateMenu();}Home();if(!testing)CheckFullscreen(true);if(!HiddenForFullscreen){Show();RenderNow(true);}Save();}
        private Size SizeFor(int scale){int ch=assets.Height;if(assets.Drag.Height>ch)ch=assets.Drag.Height;if(assets.Feed.Height>ch)ch=assets.Feed.Height;return new Size(assets.Width*scale/100,ch*scale/100);}
        internal void SetSize(int scale)
        {
            int center=Left+Width/2,bottom=Top+PetHeight;settings.Scale=Math.Max(50,Math.Min(150,scale));
            ClientSize=new Size(SizeFor(settings.Scale).Width,SizeFor(settings.Scale).Height+Renderer.BubbleHeight);
            Location=new Point(center-Width/2,bottom-PetHeight);KeepVisible();RenderNow(true);Save();UpdateMenu();
        }
        internal void Home()
        {
            Rectangle area=Screen.FromPoint(Cursor.Position).WorkingArea;
            Location=new Point(area.Right-Width-24,area.Bottom-PetHeight+(int)(12*settings.Scale/100.0));KeepVisible();
        }
        internal static Point Clamp(Point p,Size size,Rectangle area,int allowance)
        {return new Point(Math.Max(area.Left,Math.Min(p.X,area.Right-size.Width)),Math.Max(area.Top,Math.Min(p.Y,area.Bottom-size.Height+allowance)));}
        private int PetHeight{get{return Height-Renderer.BubbleHeight;}}
        private void KeepVisible()
        {
            if(gravityFalling){gravityFalling=false;Motion.Falling=false;}   // v1.7.3：任何显式摆位动作停止重力（v1.8.38：同步清惊恐）
            Rectangle area=Screen.FromRectangle(Bounds).WorkingArea;
            if(settings.Edge)Location=new Point(Math.Max(area.Left,Math.Min(Location.X,area.Right-Width)),area.Bottom-EdgeHeadPx());
            else if(edgeRight)Location=new Point(area.Right-EdgeHeadWidth()+settings.EdgeInset,Math.Max(area.Top,Math.Min(Location.Y,area.Bottom-Height)));   // v1.8.16
            else Location=Clamp(Location,new Size(Width,PetHeight),area,(int)(12*settings.Scale/100.0));
        }
        private void Save(){if(testing)return;settings.X=Left;settings.Y=Top;settings.Save();}
        // ---- v1.3：窗口坐标 → 宠物原始素材坐标（去掉气泡区偏移；镜像时翻转X）----
        private double PetX(int winX){double x=winX*100.0/settings.Scale;return settings.Mirror?assets.Width-x:x;}
        private double PetY(int winY){return (winY-Renderer.BubbleHeight)*100.0/settings.Scale;}
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if(e.Button==MouseButtons.Right){Motion.EndDrag(Now);gesture.Cancel();Capture=false;
                // v1.8.39：菜单显示在奶蛙右侧（不遮挡奶蛙）；右侧放不下（靠近屏幕右边）时自动切到左侧
                Rectangle work=Screen.FromControl(this).WorkingArea;
                int mx=Right+4,my=Top+8;
                if(mx+menu.Width>work.Right)mx=Left-menu.Width-4;   // 右边放不下 → 菜单放奶蛙左边
                if(my+menu.Height>work.Bottom)my=work.Bottom-menu.Height;
                if(my<work.Top)my=work.Top;
                menu.Show(new Point(mx,my));return;}
            if(e.Button!=MouseButtons.Left)return;
            if(edgeRight){SetEdgeRight(false);KeepVisible();}
            if(settings.Edge){settings.Edge=false;UpdateMenu();}
            gesture.Down(PointToScreen(e.Location));lastDragPointer=gesture.Origin;dragWindow=Location;Capture=true;
            gravityFalling=false;Motion.Falling=false;   // v1.8.13：抓住瞬间停重力（否则拖动与重力抢位置 → 震动）；v1.8.38：同步清惊恐
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);if(!gesture.Pressed)return;Point p=PointToScreen(e.Location);gesture.Move(p);
            if(gesture.Dragged)
            {
                if(!Motion.Holding){Motion.BeginDrag(Now);if(sound!=null)sound.Stop();}
                Motion.Drag(Now,p.X-lastDragPointer.X);
                dragVy=(p.Y-lastDragPointer.Y)*150;if(dragVy<dragVyPeak)dragVyPeak=dragVy;   // v1.8.37：系数 200→150，幅度再减半
                lastDragPointer=p;
                // v1.8.41：拖动中不再强制全量渲染（大图渲染慢导致跟手滞后/卡顿）；分层窗口位置变化由系统即时重定位，行走动画帧由 Tick 按 key 变化渲染
                Location=new Point(dragWindow.X+p.X-gesture.Origin.X,dragWindow.Y+p.Y-gesture.Origin.Y);
            }
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);if(e.Button!=MouseButtons.Left||!gesture.Pressed)return;
            bool moved=gesture.Dragged,twice=gesture.Up(PointToScreen(e.Location),Now);Capture=false;
            if(moved)
            {
                Motion.EndDrag(Now);
                // v1.7：拖到屏幕右侧边缘 → 自动贴右隐藏（旋转30°），只露头部
                Rectangle area=Screen.FromPoint(Cursor.Position).WorkingArea;
                if(!settings.Edge&&Left+Width>=area.Right-24)SetEdgeRight(true);
                else { KeepVisible();gravityFalling=true;Motion.Falling=true;gravityVy=dragVyPeak<-120?Math.Max(-1000,dragVyPeak):0;dragVyPeak=0;lastGravityTick=Now; }   // v1.8.38：上抛上限 -650→-1000（快速甩约抛起 300px 左右）
                RenderNow(true);Save();
            }
            else if(edgeRight)return;   // v1.8.14：右贴边隐藏时除拖动外不触发任何交互
            else if(twice)
            {
                // v1.8.17：双击肚子 → 捧腹大笑
                // v1.8.21：动作播放中必须双击"动作图片"（seq 帧可见像素）才推进；静止时双击眼睛下方开始
                double qx=PetX(e.X),qy=PetY(e.Y);
                if(Motion.HeadSequencing)
                {
                    double hq=settings.Scale/100.0;
                    double lx=(Width-assets.Seq.Width*hq)/2f,ty=Renderer.BubbleHeight+PetHeight-assets.Seq.Height*hq;
                    double lw=assets.Seq.Width*hq,lh=assets.Seq.Height*hq;
                    if(e.X>=lx&&e.X<lx+lw&&e.Y>=ty&&e.Y<ty+lh)
                    {
                        Bitmap sf=assets.Seq.Frame(Motion.HeadSeq-1);
                        if(sf!=null)
                        {
                            int fx=(int)((e.X-lx)/lw*sf.Width),fy=(int)((e.Y-ty)/lh*sf.Height);
                            if(fx>=0&&fy>=0&&fx<sf.Width&&fy<sf.Height&&sf.GetPixel(fx,fy).A>16){Motion.StartHeadSeq(Now);RenderNow(true);}
                        }
                    }
                }
                else if(Motion.InBelly(qx,qy))Laugh();
                else if(Motion.InHead(qx,qy)){Motion.StartHeadSeq(Now);RenderNow(true);}   // v1.8.34：双击头部全部区域 → 起势（原为眼睛下方）
            }
            else if(Motion.Resting){Motion.Stand(Now);RenderNow(true);}
            else if(Motion.HeadSequencing){/* v1.8.22：动作播放中单击不打断（否则双击第 1 次会 Poke 取消动作）*/}
            else
            {
                double px=PetX(e.X),py=PetY(e.Y);
                if(Motion.Poke(Now,px,py))RenderNow(true);
                else if(Motion.InHead(px,py)&&Motion.HeadPoke(Now))RenderNow(true);
            }
        }
        protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture&&gesture.Pressed){Motion.EndDrag(Now);gesture.Cancel();KeepVisible();RenderNow(true);Save();}}
        protected override void OnFormClosing(FormClosingEventArgs e){Save();timer.Stop();if(sound!=null)sound.Stop();base.OnFormClosing(e);}
        protected override void Dispose(bool disposingNow)
        {
            if(disposingNow&&!disposing){disposing=true;timer.Dispose();if(crowdWindows!=null){foreach(CrowdWindow c in crowdWindows)c.Close();crowdWindows=null;}if(tray!=null){tray.Visible=false;tray.Dispose();}if(menu!=null){Font f=menu.Font;menu.Dispose();f.Dispose();}if(sound!=null){sound.Stop();sound.Dispose();}if(danceSound!=null){try{danceSound.controls.stop();danceSound.close();}catch{}danceSound=null;}   // v1.8.40：释放喂食跳舞音效 COM
            surface.Dispose();if(petIcon!=null)petIcon.Dispose();}
            base.Dispose(disposingNow);
        }
    }

    // ---- v1.8.35：炸开后的小奶蛙独立画布窗口（每只奶蛙一块独立画布，各自飞行/落地/跳舞）----
    internal sealed class CrowdWindow:Form
    {
        private readonly Assets assets;
        private readonly PetWindow owner;
        private readonly int index;
        private readonly Stopwatch watch=Stopwatch.StartNew();
        private readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        private readonly LayeredSurface surface=new LayeredSurface();
        private readonly float fromX,fromY,groundY;
        private float targetX;   // 可平滑漂移（跳舞时横向游走）
        private readonly int bw,bh;
        private bool disposed;
        private bool audioStarted;   // v1.8.43：落地开始跳舞时触发一次音频（0 号小蛙播）
        protected override CreateParams CreateParams{get{CreateParams p=base.CreateParams;p.ExStyle|=0x80000|0x80|0x08000000;p.ClassStyle|=8;return p;}}   // WS_EX_LAYERED|TOOLWINDOW|NOACTIVATE（同主窗口）
        internal CrowdWindow(Assets a,int i,Point center,float ground,int petScale,PetWindow owner)
        {
            assets=a;index=i;owner=owner;
            fromX=center.X;fromY=center.Y;
            Rectangle area=Screen.FromPoint(center).WorkingArea;
            targetX=center.X+(i-2.5f)*area.Width*0.13f;   // 落点横向分布：各自飞向屏幕各处
            groundY=ground;
            bw=assets.Width*6/10*petScale/100;   // 奶蛙 0.60 显示宽
            bh=assets.Height*6/10*petScale/100;  // 奶蛙 0.60 显示高
            ClientSize=new Size(bw+24,bh+(int)(bh*1.5f)+24);   // 窗口含跳跃空间
            FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;TopMost=true;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.StandardClick|ControlStyles.StandardDoubleClick,true);
            // v1.8.39：舞群期间拖文件到小蛙上 → 取消跳舞 + 本体张嘴回收
            AllowDrop=true;
            DragEnter+=delegate(object s,DragEventArgs e){e.Effect=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;};
            DragOver+=delegate(object s,DragEventArgs e){e.Effect=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;};
            DragDrop+=delegate(object s,DragEventArgs e){if(e.Data.GetDataPresent(DataFormats.FileDrop))owner.FeedFromCrowd(e);};
            Location=new Point((int)fromX-Width/2,(int)fromY-Height/2);
            assets.Dance.Ensure(assets);
            timer.Interval=40;timer.Tick+=delegate{Tick();};timer.Start();
            Show();   // v1.8.36：必须 Show 创建窗口句柄后再 UpdateLayeredWindow（否则"参数错误"崩溃）
        }
        private void Tick()
        {
            try
            {
                if(disposed||!IsHandleCreated)return;   // 句柄未创建前跳过
                double t=watch.Elapsed.TotalSeconds;
                // 飞行：0-0.55s 从本体炸开抛物线；0.55-1.05 触地弹跳；之后地面跳舞
                double ph=Math.Min(1,t/0.55);
                double ease=1-Math.Pow(1-ph,3);   // 先慢后快：重力加速感
                double x=fromX+(targetX-fromX)*ease+Math.Sin(t*1.6+index)*6;
                double y=fromY-Math.Sin(ph*Math.PI)*130+(groundY-fromY)*ease;
                double b=0;
                if(t>=0.55){double bt=t-0.55;if(bt<1.2)b=Math.Abs(Math.Sin(bt*4.2))*55*Math.Max(0,1-bt*1.2);}   // 触地弹跳衰减
                y-=b;
                double innerY=0;
                Bitmap fr=assets.Frame(0);
                if(t>=1.05&&assets.Dance.Ready)   // 落地后：独立窗口内到处跳 + 舞蹈动画
                {
                    if(!audioStarted)
                    {
                        audioStarted=true;
                        PetWindow.LogAudio("0号跳舞分支进入 t="+t.ToString("0.00"));   // v1.8.43：确认舞群跳舞分支确实执行
                        if(index==0){try{owner.StartCrowdSound();}catch(Exception se){LogTick(se);}}   // v1.8.43：跳舞开始播放音频（只播一次，失败不阻断舞蹈）
                    }
                    Bitmap df=assets.Dance.Frame(((int)((t-1.05)*15)+index*7)%assets.Dance.Count);   // 舞步错开
                    if(df!=null)fr=df;
                    double per=1.35+0.12*(index%3);              // 跳跃周期（各不相同）
                    double u=((t-1.05+index*0.41)%per)/per;      // 相位
                    innerY=4.0*(bh*1.5f)*u*(1-u);                // 抛物线弹跳（重力感）
                    double hx=(Math.Sin(t*1.7+index*2.1)*0.36+Math.Sin(t*3.1+index*0.8)*0.15)*areaW();
                    targetX+=(float)Math.Max(-1,Math.Min(1,(hx-(x-fromX))*0.02f));   // 横向缓慢游走（平滑跟随目标漂移）
                }
                Location=new Point((int)Math.Round(x-Width/2f),(int)Math.Round(y)-Height);
                using(Bitmap frame=new Bitmap(Width,Height,PixelFormat.Format32bppPArgb))
                {
                    using(Graphics g=Graphics.FromImage(frame))
                    {
                        g.Clear(Color.Transparent);g.CompositingMode=CompositingMode.SourceCopy;g.CompositingQuality=CompositingQuality.HighSpeed;g.SmoothingMode=SmoothingMode.HighSpeed;
                        if(fr!=null)g.DrawImage(fr,(Width-bw)/2,Height-bh-(int)Math.Round(innerY),bw,bh);
                    }
                    surface.Update(Handle,frame,Location,255);
                }
            }
            catch(Exception ex)
            {
                LogTick(ex);   // v1.8.43：舞群单帧异常不弹窗不退出，记录日志后跳过本帧
                if(disposed)return;
            }
        }
        private static void LogTick(Exception ex){try{File.AppendAllText(Path.Combine(Settings.Folder,"error.log"),DateTime.Now.ToString("s")+" CrowdWindow.Tick: "+ex+Environment.NewLine);}catch{}}
        private float areaW(){return Screen.FromPoint(Location).WorkingArea.Width;}
        protected override void Dispose(bool disposingNow)
        {
            if(disposingNow&&!disposed){disposed=true;timer.Stop();timer.Dispose();surface.Dispose();}
            base.Dispose(disposingNow);
        }
    }

    internal static class Tests
    {
        private static void Check(bool value,string name,List<string> log){if(!value)throw new Exception("FAIL: "+name);log.Add("PASS: "+name);}
        private static void Advance(Motion m,double begin,double end,bool over,Assets a)
        {for(double t=begin;t<=end+.00001;t+=1.0/60)m.Update(t,over,a.Duration,a.BlinkCount/a.BlinkFps);}
        private static bool Equal(Bitmap a,Bitmap b,int fromY,int toY)
        {for(int y=fromY;y<toY;y++)for(int x=0;x<a.Width;x++)if(a.GetPixel(x,y)!=b.GetPixel(x,y))return false;return true;}
        internal static void Run(string folder)
        {
            Directory.CreateDirectory(folder);List<string> log=new List<string>();
            try
            {
                using(Assets a=new Assets(AppDomain.CurrentDomain.BaseDirectory))
                {
                    Check(a.Count==422&&Math.Abs(a.Fps-30)<.01,"original 422-frame clip at 30 fps",log);
                    Check(a.Eye(0).GetPixel(0,0).A==0&&a.Eye(1).GetPixel(0,0).A==0,"transparent idle and hover backgrounds",log);
                    int differences=0,outsideEyes=0;
                    for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++)if(a.Eye(0).GetPixel(x,y)!=a.Eye(1).GetPixel(x,y)){differences++;if(y>184)outsideEyes++;}
                    Check(differences>10&&outsideEyes==0,"hover changes only eye area, body is identical",log);
                    byte[] wav=File.ReadAllBytes(a.AudioPath);Check(wav.Length>1000&&Encoding.ASCII.GetString(wav,0,4)=="RIFF","original soundtrack decodes as WAV",log);
                    double blinkDuration=a.BlinkCount/a.BlinkFps;
                    Check(a.BlinkCount==7&&a.BlinkFps==30,"blink uses seven original video frames",log);
                    for(int bank=0;bank<2;bank++)for(int f=0;f<a.BlinkCount;f++)Check(Equal(a.Eye(bank),a.Blink(bank,f),92,a.Height),"blink body preserved, bank "+bank+" frame "+f,log);
                    Check(!Equal(a.Eye(0),a.Blink(0,3),0,92),"closed eye differs from open eye",log);
                    Motion m=new Motion();Advance(m,0,.3,true,a);Check(m.Look>.999,"hover turns eyes toward pointer",log);
                    Advance(m,.32,.62,false,a);Check(m.Look<.001,"mouse leave restores original eyes",log);
                    Check(m.Laugh(1)&&!m.Laugh(1.01)&&m.PlayCount==1,"double-click playback does not overlap or restart",log);
                    m.Update(1+a.Duration-.05,false,a.Duration,blinkDuration);Check(m.Playing,"complete source clip plays through ending",log);
                    m.Update(1+a.Duration+.01,false,a.Duration,blinkDuration);Check(!m.Playing&&!m.Resting,"laugh ends back to idle",log);
                    m.Update(100,true,a.Duration,blinkDuration);Check(!m.Resting&&!m.Playing,"idle continues after laugh",log);
                    m.Laugh(101);m.Update(101+a.Duration+.001,false,a.Duration,blinkDuration);Check(m.Laugh(120)&&!m.Resting,"can laugh again from resting pose",log);m.Stop();Check(!m.Playing&&!m.Resting,"stop action cancels playback",log);
                    m=new Motion();m.Update(4,false,a.Duration,blinkDuration);Check(m.Blinking,"idle schedules a blink",log);
                    m.Update(4+blinkDuration+.01,false,a.Duration,blinkDuration);Check(!m.Blinking&&m.NextBlink>=m.Clock+4&&m.NextBlink<=m.Clock+8,"blink ends and schedules random 4-8 second interval",log);
                    m.Poke(5,143,211);Check(m.Poking&&!m.Blinking,"poke interrupts blink",log);m.Update(5.1,false,a.Duration,blinkDuration);
                    using(Bitmap warped=Renderer.WarpBelly(a.Eye(0),m.PokeX,m.PokeY,m.PokeAmount))
                    {Check(Equal(warped,a.Eye(0),0,105),"belly feedback preserves face",log);Check(!Equal(warped,a.Eye(0),160,260),"belly feedback visibly deforms source pixels",log);}
                    Advance(m,5.11,6,false,a);Check(!m.Poking&&Math.Abs(m.PokeAmount)<.0001,"belly rebound settles",log);
                    Check(!m.Poke(6,125,67),"clicking face does not poke belly",log);
                    m.Poke(6,144,210);m.Laugh(6.1);Check(!m.Poking&&m.Playing,"double click cancels poke before original laugh",log);
                    m.BeginDrag(7);Check(m.Holding&&!m.Playing,"drag interrupts playback",log);
                    for(int i=1;i<=30;i++){m.Drag(7+i/60.0,12);m.Update(7+i/60.0,true,a.Duration,blinkDuration);}
                    Check(m.DragAngle>2&&m.DragAngle<=7,"drag tilts toward movement and stays bounded",log);
                    m.EndDrag(7.5);m.Update(7.57,true,a.Duration,blinkDuration);Check(!m.Holding&&m.Landing&&m.Squash>.01,"release starts landing bounce",log);
                    Advance(m,7.58,9,false,a);Check(!m.Landing&&Math.Abs(m.DragAngle)<.05,"drag and landing settle cleanly",log);
                    Gesture gesture=new Gesture();Point p=new Point(100,100);gesture.Down(p);Check(!gesture.Up(p,1),"single click does not laugh",log);gesture.Down(p);Check(gesture.Up(p,1.12),"double click triggers laugh",log);
                    gesture.Down(p);gesture.Move(new Point(140,160));Check(!gesture.Up(new Point(140,160),2),"drag does not trigger laugh",log);
                    gesture.Down(p);Check(!gesture.Up(p,2.1),"drag resets double-click sequence",log);gesture.Cancel();Check(!gesture.Pressed,"cancelled pointer capture releases drag",log);
                    Point clamped=PetWindow.Clamp(new Point(-3000,-50),new Size(245,312),new Rectangle(-1920,0,1920,1040),10);Check(clamped.X==-1920&&clamped.Y==0,"negative-coordinate monitor bounds",log);
                    Rectangle monitor=new Rectangle(0,0,1920,1080);
                    Check(Fullscreen.Covers(monitor,monitor,0,"FullscreenPlayer"),"borderless fullscreen is detected",log);
                    Check(!Fullscreen.Covers(monitor,monitor,0x00c00000,"NormalWindow"),"ordinary maximized window is ignored",log);
                    Check(!Fullscreen.Covers(new Rectangle(0,0,1920,1040),monitor,0,"Video"),"window that leaves taskbar visible is not fullscreen",log);
                    Check(!Fullscreen.Covers(new Rectangle(-1920,0,1920,1080),monitor,0,"Video"),"fullscreen on another monitor is ignored",log);
                    Check(Fullscreen.Covers(new Rectangle(-1920,0,1920,1080),new Rectangle(-1920,0,1920,1080),0,"Video"),"fullscreen works on negative-coordinate monitor",log);
                    Check(!Fullscreen.Covers(monitor,monitor,0,"WorkerW"),"desktop background is not fullscreen app",log);
                    FullscreenGate gate=new FullscreenGate();Check(!gate.Update(true,0)&&!gate.Update(false,.08)&&!gate.Hidden,"brief fullscreen switch does not flash pet",log);
                    gate.Update(true,1);Check(gate.Update(true,1.2)&&gate.Hidden,"stable fullscreen hides pet",log);gate.Update(false,2);Check(gate.Update(false,2.3)&&!gate.Hidden,"leaving fullscreen restores pet",log);
                    using(PetWindow form=new PetWindow(a,true))
                    {
                        form.Show();Application.DoEvents();form.RenderNow(true);
                        Check(Native.IsWindowVisible(form.Handle),"native pet window is visible",log);
                        int style=Native.GetWindowLong(form.Handle,-20);Check((style&0x80000)!=0&&(style&0x80)!=0&&(style&0x08000000)!=0,"alpha transparency, no taskbar entry, no focus stealing",log);
                        form.SetSize(65);Check(form.Width==a.Width*65/100,"small size renders",log);form.SetSize(110);Check(form.Width==a.Width*110/100,"large size renders",log);form.SetSize(85);
                        form.Location=new Point(-50000,-50000);form.SetSize(85);Check(Screen.FromRectangle(form.Bounds).WorkingArea.IntersectsWith(form.Bounds),"lost window position recovers",log);
                        form.SetSound(true);Check(form.SoundEnabled,"original audio switch can be enabled",log);form.SetSound(false);Check(!form.SoundEnabled,"sound switch can be muted",log);
                        IntPtr mousePoint=(IntPtr)((218<<16)|120); // 窗口坐标：素材肚子处（窗口顶部含58px气泡区）
                        Native.SendMessage(form.Handle,0x201,(IntPtr)1,mousePoint);Native.SendMessage(form.Handle,0x202,IntPtr.Zero,mousePoint);
                        Check(!form.Motion.Playing&&form.Motion.Poking,"native single belly click triggers poke",log);
                        Native.SendMessage(form.Handle,0x203,(IntPtr)1,mousePoint);Native.SendMessage(form.Handle,0x202,IntPtr.Zero,mousePoint);
                        Check(form.Motion.Playing&&!form.Motion.Poking,"native double-click events replace poke with playback",log);form.StopLaugh();
                        // v1.8.22：双击起势图推进下一阶段验证（真实消息序列 Down→Up→DblClk→Up）
                        form.Motion.ClearEffects(0);   // 清睡眠等阻塞状态（自测窗口 tick 可能已触发 Sleep）
                        form.Motion.StartHeadSeq(0);form.RenderNow(true);Check(form.Motion.HeadSequencing&&form.Motion.HeadSeq==1,"seq stage1 starts",log);
                        IntPtr seqPt=(IntPtr)((150<<16)|110);   // 起势图中央（窗口坐标 y150 x110，在 seq 图内）
                        Native.SendMessage(form.Handle,0x201,(IntPtr)1,seqPt);Native.SendMessage(form.Handle,0x202,IntPtr.Zero,seqPt);
                        Native.SendMessage(form.Handle,0x203,(IntPtr)1,seqPt);Native.SendMessage(form.Handle,0x202,IntPtr.Zero,seqPt);
                        Check(form.Motion.HeadSeq==2,"native double-click on seq image advances stage",log);
                        Native.SendMessage(form.Handle,0x201,(IntPtr)1,seqPt);Native.SendMessage(form.Handle,0x202,IntPtr.Zero,seqPt);
                        Native.SendMessage(form.Handle,0x203,(IntPtr)1,seqPt);Native.SendMessage(form.Handle,0x202,IntPtr.Zero,seqPt);
                        Check(form.Motion.HeadSeq==3,"native double-click on seq image advances to stage3",log);form.Motion.EndHeadSeq();form.RenderNow(true);
                        form.Motion.Laugh(0);form.Motion.Update(a.Duration,false,a.Duration,blinkDuration);Check(!form.Motion.Playing&&!form.Motion.Resting,"native laugh returns to idle",log);
                        IntPtr footPoint=(IntPtr)((298<<16)|170); // 窗口坐标：素材脚部
                        Native.SendMessage(form.Handle,0x201,(IntPtr)1,footPoint);Native.SendMessage(form.Handle,0x202,IntPtr.Zero,footPoint);
                        Check(!form.Motion.Playing,"native click after laugh stays idle",log);form.StopLaugh();
                        Native.SendMessage(form.Handle,0x201,(IntPtr)1,mousePoint);
                        Native.SendMessage(form.Handle,0x200,(IntPtr)1,(IntPtr)((180<<16)|155));Check(form.Motion.Holding,"native drag starts feedback",log);
                        Native.SendMessage(form.Handle,0x202,IntPtr.Zero,mousePoint);Check(!form.Motion.Holding&&form.Motion.Landing&&!form.Motion.Playing,"native drag release bounces without laughing",log);form.StopLaugh();
                        using(Form fullscreen=new Form())
                        {
                            fullscreen.FormBorderStyle=FormBorderStyle.None;fullscreen.StartPosition=FormStartPosition.Manual;fullscreen.ShowInTaskbar=false;fullscreen.Opacity=.01;
                            fullscreen.Bounds=Screen.FromRectangle(form.Bounds).Bounds;fullscreen.Show();Application.DoEvents();
                            Check(Fullscreen.IsActive(fullscreen.Handle,form.Handle,fullscreen.Bounds),"native borderless fullscreen detection",log);
                            Check(!Fullscreen.IsActive(form.Handle,form.Handle,fullscreen.Bounds),"pet window is excluded from fullscreen detection",log);
                            // Supply the actual test HWND; sandbox desktops cannot take foreground focus.
                            form.TickWithForeground(fullscreen.Handle);Thread.Sleep(220);form.TickWithForeground(fullscreen.Handle);Check(form.HiddenForFullscreen&&!Native.IsWindowVisible(form.Handle),"native fullscreen detection automatically hides pet through timer",log);
                            fullscreen.Hide();Check(!Fullscreen.IsActive(fullscreen.Handle,form.Handle,fullscreen.Bounds),"hidden window is excluded from fullscreen detection",log);
                            Thread.Sleep(180);form.TickWithForeground(fullscreen.Handle);Thread.Sleep(300);form.TickWithForeground(fullscreen.Handle);
                            Check(!form.HiddenForFullscreen&&Native.IsWindowVisible(form.Handle),"leaving fullscreen automatically restores pet through timer",log);fullscreen.Close();
                        }
                        Point savedLocation=form.Location;form.Laugh();form.ApplyFullscreen(true);
                        Check(form.HiddenForFullscreen&&!Native.IsWindowVisible(form.Handle)&&!form.Motion.Playing,"fullscreen hides native pet and interrupts playback",log);
                        Check(!form.Laugh(),"hidden pet cannot play audio or laugh",log);
                        form.ApplyFullscreen(false);Check(!form.HiddenForFullscreen&&Native.IsWindowVisible(form.Handle)&&form.Location==savedLocation,"fullscreen exit restores same position",log);
                        form.Motion.Laugh(0);form.Motion.Update(a.Duration,false,a.Duration,blinkDuration);form.ApplyFullscreen(true);form.ApplyFullscreen(false);
                        Check(!form.Motion.Playing&&!form.Motion.Resting,"laugh state survives fullscreen hide and restore",log);
                        form.ApplyFullscreen(true);form.SetAutoHide(false);Check(Native.IsWindowVisible(form.Handle)&&!form.HiddenForFullscreen,"disabling automatic hide reveals pet",log);form.StopLaugh();
                        // Fill the bounded cache before measuring steady-state resource use.
                        for(int i=0;i<12;i++)a.Frame(i);
                        int before=Native.GetGuiResources(Process.GetCurrentProcess().Handle,0);Stopwatch sw=Stopwatch.StartNew();
                        form.Motion.Laugh(0);
                        for(int i=0;i<a.Count;i++){form.Motion.Elapsed=i/a.Fps;form.RenderNow(true);}
                        sw.Stop();int after=Native.GetGuiResources(Process.GetCurrentProcess().Handle,0);
                        log.Add("METRIC: all-frame decode/render average "+(sw.Elapsed.TotalMilliseconds/a.Count).ToString("F2")+" ms; GDI delta "+(after-before));
                        // GDI+ lazily initializes renderer resources on its first complete playback.
                        for(int i=0;i<a.Count;i++){form.Motion.Elapsed=i/a.Fps;form.RenderNow(true);}
                        int stable=Native.GetGuiResources(Process.GetCurrentProcess().Handle,0);
                        log.Add("METRIC: repeat-playback GDI delta "+(stable-after));
                        Check(stable-after<10,"repeat playback does not leak GDI handles",log);Check(a.CacheCount<=12,"decoded frame cache stays bounded",log);
                        form.Motion.Stop();form.RenderNow(true);form.Close();Check(form.IsDisposed,"window closes cleanly",log);
                    }
                    using(Icon icon=Renderer.CreateIcon(a.Eye(0)))using(FileStream f=File.Create(Path.Combine(folder,"naiwa.ico")))icon.Save(f);
                    m=new Motion();
                    using(Bitmap b=Renderer.Render(a,m,100))b.Save(Path.Combine(folder,"native-idle.png"),ImageFormat.Png);
                    m.Look=1;using(Bitmap b=Renderer.Render(a,m,100))b.Save(Path.Combine(folder,"native-hover.png"),ImageFormat.Png);
                    m.Laugh(0);m.Elapsed=1;using(Bitmap b=Renderer.Render(a,m,100))b.Save(Path.Combine(folder,"native-laugh.png"),ImageFormat.Png);
                    m=new Motion();m.Poke(0,144,211);m.Update(.10,false,a.Duration,blinkDuration);using(Bitmap b=Renderer.Render(a,m,100))b.Save(Path.Combine(folder,"native-poke.png"),ImageFormat.Png);
                    m.BeginDrag(1);m.DragAngle=6;using(Bitmap b=Renderer.Render(a,m,100))b.Save(Path.Combine(folder,"native-drag.png"),ImageFormat.Png);
                    m=new Motion();m.BlinkStart=0;m.Clock=.1;using(Bitmap b=Renderer.Render(a,m,100))b.Save(Path.Combine(folder,"native-blink.png"),ImageFormat.Png);
                    m.Stop();m.Resting=true;using(Bitmap b=Renderer.Render(a,m,100))b.Save(Path.Combine(folder,"native-rest.png"),ImageFormat.Png);
                    // v1.6.4：沮丧表情渲染（feed.nwp 解码 + 尺寸正确）
                    m=new Motion();m.FeedStart=0;Renderer.Render(a,m,100);a.Feed.Ensure(a);
                    Check(a.Feed.Ready,"feed art decodes",log);
                    if(a.Feed.Ready){using(Bitmap fb=Renderer.Render(a,m,100)){Check(fb.Width==a.Width&&fb.Height==a.Height+Renderer.BubbleHeight,"feed renders into standard canvas",log);fb.Save(Path.Combine(folder,"native-feed.png"),ImageFormat.Png);}}
                    // v1.6.4：拖放素材画布宽度与素材一致；v1.6.8 张嘴/回收动画奶蛙高 350（画布 350>素材 309）
                    if(a.Drag.Width>0)Check(a.Drag.Width==a.Width,"drag art width unified with native size",log);
                    m=new Motion();m.HoverOpen=true;m.Clock=0;using(Bitmap b=Renderer.Render(a,m,100))b.Save(Path.Combine(folder,"native-hoveropen.png"),ImageFormat.Png);
                    // v1.8：行走动画解码与渲染
                    m=new Motion();m.BeginDrag(1);Renderer.Render(a,m,100);a.Walk.Ensure(a);
                    Check(a.Walk.Ready&&a.Walk.Count==8,"walk art decodes 8 frames",log);
                    // v1.8.9：拖起奶蛙（惊讶表情）素材
                    a.Hold.Ensure(a);Check(a.Hold.Ready&&a.Hold.Count==1,"hold art decodes 1 frame",log);
                    if(a.Walk.Ready){m.BeginDrag(0);m.Clock=0;using(Bitmap b=Renderer.Render(a,m,100))b.Save(Path.Combine(folder,"native-walk.png"),ImageFormat.Png);}
                    // v1.8.17：三阶段动作素材解码与渲染验证（1起势/2愤怒/3打电话）
                    a.Seq.Ensure(a);Check(a.Seq.Ready&&a.Seq.Count==3,"seq art decodes 3 frames",log);
                    if(a.Seq.Ready)
                    {
                        m=new Motion();m.HeadSeqStart=0;m.HeadSeq=1;m.Clock=0;
                        using(Bitmap s1=Renderer.Render(a,m,100)){int so=0;for(int y=0;y<s1.Height;y++)for(int x=0;x<s1.Width;x++)if(s1.GetPixel(x,y).A>16){so++;if(so>5000)break;}Check(so>5000,"seq frame1 renders non-empty",log);s1.Save(Path.Combine(folder,"native-seq1.png"),ImageFormat.Png);}
                        m.HeadSeq=3;m.Clock=1;using(Bitmap s3=Renderer.Render(a,m,100))s3.Save(Path.Combine(folder,"native-seq3.png"),ImageFormat.Png);
                        m.Clock=3;using(Bitmap sd=Renderer.Render(a,m,100))sd.Save(Path.Combine(folder,"native-seqdance.png"),ImageFormat.Png);
                    }
                    // v1.8.1：落地行走帧推进验证（两帧像素必须不同）
                    m=new Motion();m.StartWalk(0,1);
                    using(Bitmap wf0=Renderer.Render(a,m,100))
                    {
                        // v1.8.3：行走分支必须能自行加载素材并画出帧（不依赖外部 Ensure）
                        int opaque=0;
                        for(int y=0;y<wf0.Height;y++)for(int x=0;x<wf0.Width;x++)if(wf0.GetPixel(x,y).A>16){opaque++;if(opaque>5000)break;}
                        Check(opaque>5000,"walk branch renders frames via internal Ensure",log);
                        m.Update(.12,false,a.Duration,blinkDuration);
                        using(Bitmap wf1=Renderer.Render(a,m,100))
                        {
                            int wd=0;
                            for(int y=0;y<Math.Min(wf0.Height,wf1.Height);y++)for(int x=0;x<Math.Min(wf0.Width,wf1.Width);x++)if(wf0.GetPixel(x,y)!=wf1.GetPixel(x,y)){wd++;if(wd>50)break;}
                            Check(wd>50,"walk frames advance while walking",log);
                        }
                    }
                    // v1.7.2：右贴边旋转预览（-30° 逆时针，检查只露头效果）
                    m=new Motion();using(Bitmap b=Renderer.Render(a,m,100,false,100,null,true))b.Save(Path.Combine(folder,"native-rotate.png"),ImageFormat.Png);
                }
                log.Add("RESULT: PASS");
            }
            catch(Exception e){log.Add(e.ToString());log.Add("RESULT: FAIL");Environment.ExitCode=1;}
            File.WriteAllLines(Path.Combine(folder,"self-test.txt"),log.ToArray(),Encoding.UTF8);
        }
        internal static void Demo(string folder)
        {
            Directory.CreateDirectory(folder);using(Assets a=new Assets(AppDomain.CurrentDomain.BaseDirectory))
            {
                Motion m=new Motion();m.NextBlink=.6;
                for(int i=0;i<720;i++)
                {
                    double t=i/30.0;
                    if(i==60)m.Poke(t,143,205);
                    if(i==90)m.BeginDrag(t);
                    if(i>=90&&i<126)m.Drag(t,i<108?5:-5);
                    if(i==126)m.EndDrag(t);
                    if(i==174)m.Laugh(t);
                    if(i==666)m.Stand(t);
                    m.Update(t,t>=1.1&&t<5.8,a.Duration,a.BlinkCount/a.BlinkFps);
                    using(Bitmap b=Renderer.Render(a,m,100))b.Save(Path.Combine(folder,i.ToString("0000")+".png"),ImageFormat.Png);
                }
            }
        }
        // ---- v1.6：拖放回收链路测试（会真的把测试文件移入回收站）----
        internal static void DropTest(string file)
        {
            string log=Path.Combine(Settings.Folder,"drop-test.txt");
            try
            {
                Directory.CreateDirectory(Settings.Folder);
                using(Assets a=new Assets(AppDomain.CurrentDomain.BaseDirectory))
                using(PetWindow form=new PetWindow(a,true))
                {
                    form.Show();Application.DoEvents();form.RenderNow(true);
                    bool existedBefore=File.Exists(file);
                    DataObject data=new DataObject();
                    data.SetData(DataFormats.FileDrop,new string[]{file});
                    // v1.6.2：悬停张嘴验证（DragEnter → 张嘴循环帧；DragLeave → 恢复）
                    form.SimulateHoverEnter();
                    Application.DoEvents();form.RenderNow(true);
                    bool hoverOpen=form.Motion.HoverOpen;
                    bool hoverRendered=false;
                    if(hoverOpen&&a.Drag.Ready)
                    {
                        Motion hm=new Motion();hm.HoverOpen=true;hm.Clock=0;
                        Motion im2=new Motion();im2.Clock=0;
                        using(Bitmap hf=Renderer.Render(a,hm,100))
                        using(Bitmap bf2=Renderer.Render(a,im2,100))
                        {
                            int diff=0,hh=Math.Min(hf.Height,bf2.Height);
                            BitmapData hd=hf.LockBits(new Rectangle(0,0,hf.Width,hh),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                            BitmapData bd2=bf2.LockBits(new Rectangle(0,0,bf2.Width,hh),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                            try
                            {
                                int len=hd.Stride*hh;byte[] hb=new byte[len],bb2=new byte[len];
                                Marshal.Copy(hd.Scan0,hb,0,len);Marshal.Copy(bd2.Scan0,bb2,0,len);
                                for(int k=0;k<len;k+=4)if(Math.Abs(hb[k]-bb2[k])+Math.Abs(hb[k+1]-bb2[k+1])+Math.Abs(hb[k+2]-bb2[k+2])+Math.Abs(hb[k+3]-bb2[k+3])>60)diff++;
                            }
                            finally{hf.UnlockBits(hd);bf2.UnlockBits(bd2);}
                            hoverRendered=diff>500;
                        }
                    }
                    form.SimulateHoverLeave();
                    Application.DoEvents();
                    bool hoverClosed=!form.Motion.HoverOpen;
                    // v1.6.5：拖走（未放下）→ 沮丧表情出现
                    bool feedingAfterLeave=form.Motion.Feeding;
                    form.OnFileDrop(null,new DragEventArgs(data,0,0,0,DragDropEffects.Copy,DragDropEffects.Copy));
                    Application.DoEvents();form.RenderNow(true);
                    bool dragging=form.Motion.Dragging;
                    // v1.6.5：放下后沮丧表情被清除（StartDrag 清 FeedStart）
                    bool feedingAfterDrop=form.Motion.Feeding;
                    // 真实渲染验证：拖放动画帧 vs 待机帧的像素差异
                    bool dragRendered=false;
                    if(dragging&&a.Drag.Ready)
                    {
                        Motion dm=new Motion();dm.DragStart=0;dm.DragDuration=a.Drag.Duration;dm.Clock=0;
                        Motion im=new Motion();im.Clock=0;
                        using(Bitmap df=Renderer.Render(a,dm,100))
                        using(Bitmap bf=Renderer.Render(a,im,100))
                        {
                            int diff=0,hh2=Math.Min(df.Height,bf.Height);
                            BitmapData dd=df.LockBits(new Rectangle(0,0,df.Width,hh2),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                            BitmapData bd=bf.LockBits(new Rectangle(0,0,bf.Width,hh2),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                            try
                            {
                                int len=dd.Stride*hh2;byte[] db=new byte[len],bb=new byte[len];
                                Marshal.Copy(dd.Scan0,db,0,len);Marshal.Copy(bd.Scan0,bb,0,len);
                                for(int k=0;k<len;k+=4)if(Math.Abs(db[k]-bb[k])+Math.Abs(db[k+1]-bb[k+1])+Math.Abs(db[k+2]-bb[k+2])+Math.Abs(db[k+3]-bb[k+3])>60)diff++;
                            }
                            finally{df.UnlockBits(dd);bf.UnlockBits(bd);}
                            dragRendered=diff>500;
                        }
                    }
                    bool existsAfter=File.Exists(file);
                    File.WriteAllText(log,"ExistedBefore="+existedBefore+"\r\nHoverOpen="+hoverOpen+"\r\nHoverRendered="+hoverRendered+"\r\nHoverClosed="+hoverClosed+"\r\nFeedingAfterLeave="+feedingAfterLeave+"\r\nDragging="+dragging+"\r\nDragRendered="+dragRendered+"\r\nFeedingAfterDrop="+feedingAfterDrop+"\r\nFileExistsAfter="+existsAfter+"\r\nRESULT="+(existedBefore&&hoverOpen&&hoverRendered&&hoverClosed&&feedingAfterLeave&&dragging&&dragRendered&&!feedingAfterDrop&&!existsAfter?"PASS":"FAIL"),Encoding.UTF8);
                    Console.WriteLine("drop-test: hoverOpen="+hoverOpen+" hoverRendered="+hoverRendered+" hoverClosed="+hoverClosed+" feedingAfterLeave="+feedingAfterLeave+" dragging="+dragging+" dragRendered="+dragRendered+" feedingAfterDrop="+feedingAfterDrop+" existsAfter="+existsAfter);
                    form.Close();
                }
            }
            catch(Exception e){try{File.WriteAllText(log,"ERR "+e,Encoding.UTF8);}catch{}Console.WriteLine("drop-test ERR: "+e.Message);}
        }
    }
}
