using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NaiwaDesktop;

internal sealed class PetWindow : Form
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct SHFILEOPSTRUCT
	{
		public IntPtr hwnd;

		public uint wFunc;

		public string pFrom;

		public IntPtr pTo;

		public ushort fFlags;

		public int fAnyOperationsAborted;

		public IntPtr hNameMappings;

		public string lpszProgressTitle;
	}

	private readonly Assets assets;

	private readonly bool testing;

	private readonly Settings settings;

	internal readonly Motion Motion = new Motion();

	private readonly Gesture gesture = new Gesture();

	private readonly Stopwatch watch = Stopwatch.StartNew();

	private readonly Timer timer = new Timer();

	private readonly LayeredSurface surface = new LayeredSurface();

	private dynamic laughSound;

	private readonly Icon petIcon;

	private readonly ContextMenuStrip menu;

	private double menuOpenedAt;

	private double menuMouseHoverAt;

	private bool wasMenuLButton;

	private bool danceInterrupted;

	private dynamic danceSound;

	private bool danceFading;

	private bool crowdSoundStarted;

	private double crowdCreatedAt = -1.0;

	private double danceFadeStart;

	private static readonly string DanceAudio = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dance.mp3");

	private static readonly string CryAudio = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cry.wav");

	private dynamic crySound;

	private bool fatBubbleShown;

	private readonly NotifyIcon tray;

	private ToolStripMenuItem fullscreenItem;

	private ToolStripMenuItem autoRunItem;

	private ToolStripMenuItem hungerItem;

	private ToolStripMenuItem soundItem;

	private ToolStripMenuItem volumeItem;

	private ToolStripMenuItem volumeMuteItem;

	private TrackBar volumeBarControl;

	private int volumeMuteBack = 100;

	private Point dragWindow;

	private Point lastDragPointer;

	private readonly FullscreenGate fullscreenGate = new FullscreenGate();

	private double nextFullscreenCheck;

	private string lastVisual = "";

	private bool disposing;

	private bool menuOpen;

	private bool ready;

	private List<CrowdWindow> crowdWindows;

	private bool gravityFalling;

	private double gravityVy;

	private double lastGravityTick;

	private double lastWalkTick;

	private double dragVy;

	private double dragVyPeak;

	private string bubbleText;

	private double bubbleUntil;

	private double nextWalk = 8.0;

	private double lastHover = -1000000000.0;

	private double nextIdle = 45.0;

	private double nextIdleRoll = 60.0;

	private double nextSleepRoll = 360.0;

	private double reminderNext = -1.0;

	private int lastChimeHour;

	private readonly Random extraRandom = new Random();

	private byte[] sadMask;

	private int sadW;

	private int sadH;

	private bool edgeRight;

	private static readonly DateTimeOffset UnixEpoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

	internal string LastVisual => lastVisual;

	internal bool HiddenForFullscreen { get; private set; }

	internal int ScalePercent => settings.Scale;

	internal bool SoundEnabled => settings.Sound;

	private double Now => watch.Elapsed.TotalSeconds;

	protected override bool ShowWithoutActivation => true;

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams createParams = base.CreateParams;
			createParams.ExStyle |= 134742144;
			createParams.ClassStyle |= 8;
			return createParams;
		}
	}

	private int PetHeight => Height - 58;

	internal PetWindow(Assets a, bool test)
	{
		assets = a;
		testing = test;
		settings = (testing ? new Settings() : Settings.Load());
		Motion.AutoLook = true;
		if (settings.HungerStamp <= 0)
		{
			settings.HungerStamp = (long)(DateTimeOffset.UtcNow - UnixEpoch).TotalSeconds + 600;
		}
		Text = "NaiwaDesktop.Source.v1";
		FormBorderStyle = FormBorderStyle.None;
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.Manual;
		AutoScaleMode = AutoScaleMode.None;
		SetStyle(ControlStyles.UserPaint | ControlStyles.StandardClick | ControlStyles.StandardDoubleClick | ControlStyles.AllPaintingInWmPaint, value: true);
		ClientSize = new Size(SizeFor(settings.Scale).Width, SizeFor(settings.Scale).Height + 58);
		TopMost = settings.OnTop;
		Cursor = Cursors.Hand;
		AllowDrop = true;
		DragEnter += (object s, DragEventArgs e) =>
		{
			e.Effect = (e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None);
			if (e.Effect != DragDropEffects.None && PixelHit(e))
			{
				OnHoverEnter();
			}
		};
		DragOver += (object s, DragEventArgs e) =>
		{
			e.Effect = (e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None);
			if (e.Effect != DragDropEffects.None)
			{
				if (PixelHit(e))
				{
					if (!Motion.HoverOpen)
					{
						OnHoverEnter();
					}
				}
				else
				{
					OnHoverLeave();
				}
			}
		};
		DragLeave += (object s, EventArgs e) =>
		{
			OnHoverLeave();
		};
		DragDrop += OnFileDrop;
		if (settings.Position)
		{
			Location = new Point(settings.X, settings.Y);
		}
		else
		{
			Home();
		}
		KeepVisible();
		petIcon = Renderer.CreateIcon(assets.Eye(0.0));
		Icon = petIcon;
		if (File.Exists(assets.AudioPath))
		{
			try
			{
				EnsureLaughPlayer();
			}
			catch
			{
			}
		}
		menu = BuildMenu();
		if (!testing)
		{
			tray = new NotifyIcon
			{
				Icon = petIcon,
				Text = "奶蛙 · 戳肚子 / 双击笑 / 拖动",
				ContextMenuStrip = menu,
				Visible = true
			};
			tray.DoubleClick += (object param0, EventArgs param1) =>
			{
				ReturnHome();
			};
		}
		try
		{
			File.AppendAllText(Path.Combine(Settings.Folder, "start.log"), DateTime.Now.ToString("s") + " ctor-ok visible=" + Visible + " handle=" + IsHandleCreated + Environment.NewLine);
		}
		catch
		{
		}
		timer.Interval = 16;
		timer.Tick += (object param0, EventArgs param1) =>
		{
			Tick();
		};
		Shown += (object param0, EventArgs param1) =>
		{
			ready = true;
			lastChimeHour = DateTime.Now.Hour;
			if (settings.ReminderMinutes > 0)
			{
				reminderNext = Now + (double)(settings.ReminderMinutes * 60);
			}
			RenderNow(force: true);
			if (!testing)
			{
				CheckFullscreen(immediate: true);
				timer.Start();
			}
			try
			{
				File.AppendAllText(Path.Combine(Settings.Folder, "start.log"), DateTime.Now.ToString("s") + " shown-ok visible=" + Visible + " handle=" + IsHandleCreated + Environment.NewLine);
			}
			catch
			{
			}
		};
	}

	protected override void WndProc(ref Message m)
	{
		if (m.Msg == 32769)
		{
			ReturnHome();
			return;
		}
		if (m.Msg == 32770)
		{
			ref Message reference = ref m;
			int num;
			if (HiddenForFullscreen)
			{
				num = 4;
			}
			else if (Motion.Resting)
			{
				num = 3;
			}
			else
			{
				num = (Motion.Playing ? 2 : ((Motion.Look > 0.5) ? 1 : 0));
			}
			reference.Result = (IntPtr)num;
			return;
		}
		if (m.Msg == 32771)
		{
			EatDown();
			return;
		}
		if (m.Msg == 33)
		{
			m.Result = (IntPtr)3;
			return;
		}
		if (m.Msg == 132 && ready && !gesture.Pressed)
		{
			long num2 = m.LParam.ToInt64();
			Point point = PointToClient(new Point((short)(num2 & 0xFFFF), (short)((num2 >> 16) & 0xFFFF)));
			m.Result = (surface.Hit(point.X, point.Y) ? ((IntPtr)1) : new IntPtr(-1));
			return;
		}
		base.WndProc(ref m);
		if (m.Msg == 126 && ready)
		{
			KeepVisible();
			RenderNow(force: true);
			Save();
		}
	}

	private ContextMenuStrip BuildMenu()
	{
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
		contextMenuStrip.Font = new Font("Microsoft YaHei UI", 10f);
		contextMenuStrip.ShowImageMargin = false;
		ContextMenuStrip contextMenuStrip2 = contextMenuStrip;
		hungerItem = new ToolStripMenuItem("饥饿：");
		contextMenuStrip2.Items.Insert(0, hungerItem);
		contextMenuStrip2.Items.Add("使用说明书", null, (object param0, EventArgs param1) =>
		{
			Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "使用说明.txt"))
			{
				UseShellExecute = true
			});
		});
		contextMenuStrip2.Items.Add(new ToolStripSeparator());
		ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem("设置");
		ToolStripMenuItem toolStripMenuItem2 = new ToolStripMenuItem("查看动作");
		string[,] array = new string[12, 2]
		{
			{ "大笑", "laugh" },
			{ "小动作", "idle" },
			{ "转手舞", "idle5" },
			{ "哭泣（上抛后）", "idle6" },
			{ "行走", "walk" },
			{ "起势（第1阶段）", "seq1" },
			{ "愤怒（第2阶段）", "seq2" },
			{ "打电话·炸开舞群", "seq3" },
			{ "喂食庆祝舞蹈", "dance" },
			{ "饥饿乞讨", "hungry" },
			{ "吃饱变袋鼠", "fat" },
			{ "睡觉", "sleep" }
		};
		for (int num = 0; num < array.GetLength(0); num++)
		{
			string text = array[num, 0];
			string key = array[num, 1];
			toolStripMenuItem2.DropDownItems.Add(text, null, (object param0, EventArgs param1) =>
			{
				DemoAction(key);
			});
		}
		toolStripMenuItem.DropDownItems.Add(toolStripMenuItem2);
		ToolStripMenuItem toolStripMenuItem3 = new ToolStripMenuItem("大小");
		int[] array2 = new int[8] { 50, 65, 80, 95, 110, 125, 140, 150 };
		string[] array3 = new string[8] { "很小(50%)", "小号(65%)", "中号(80%)", "标准(95%)", "大号(110%)", "加大(125%)", "特大(140%)", "最大(150%)" };
		for (int num2 = 0; num2 < array2.Length; num2++)
		{
			int v = array2[num2];
			ToolStripMenuItem toolStripMenuItem4 = new ToolStripMenuItem(array3[num2], null, (object param0, EventArgs param1) =>
			{
				SetSize(v);
			});
			toolStripMenuItem4.Tag = v;
			toolStripMenuItem3.DropDownItems.Add(toolStripMenuItem4);
		}
		toolStripMenuItem3.DropDownItems.Add(new ToolStripSeparator());
		toolStripMenuItem3.DropDownItems.Add("放大 5%", null, (object param0, EventArgs param1) =>
		{
			SetSize(settings.Scale + 5);
		});
		toolStripMenuItem3.DropDownItems.Add("缩小 5%", null, (object param0, EventArgs param1) =>
		{
			SetSize(settings.Scale - 5);
		});
		toolStripMenuItem.DropDownItems.Add(toolStripMenuItem3);
		soundItem = new ToolStripMenuItem("声音", null, (object param0, EventArgs param1) =>
		{
			SetSound(!settings.Sound);
		});
		toolStripMenuItem.DropDownItems.Add(soundItem);
		volumeItem = new ToolStripMenuItem("音量：" + settings.Volume + "%");
		TrackBar volumeBar = new TrackBar
		{
			Minimum = 0,
			Maximum = 100,
			TickStyle = TickStyle.None,
			SmallChange = 5,
			LargeChange = 10,
			AutoSize = false,
			Height = 34,
			Width = 200,
			Value = settings.Volume
		};
		volumeBar.Scroll += (object param0, EventArgs param1) =>
		{
			SetVolume(volumeBar.Value);
		};
		volumeBarControl = volumeBar;
		ToolStripControlHost toolStripControlHost = new ToolStripControlHost(volumeBar);
		toolStripControlHost.AutoSize = false;
		toolStripControlHost.Width = 210;
		toolStripControlHost.Height = 34;
		ToolStripControlHost value = toolStripControlHost;
		volumeItem.DropDownItems.Add(value);
		volumeItem.DropDownItems.Add(new ToolStripSeparator());
		volumeItem.DropDownItems.Add("调大 5%", null, (object param0, EventArgs param1) =>
		{
			SetVolume(settings.Volume + 5);
		});
		volumeItem.DropDownItems.Add("调小 5%", null, (object param0, EventArgs param1) =>
		{
			SetVolume(settings.Volume - 5);
		});
		volumeMuteItem = new ToolStripMenuItem("静音", null, (object param0, EventArgs param1) =>
		{
			ToggleMute();
		});
		volumeItem.DropDownItems.Add(volumeMuteItem);
		volumeItem.DropDownItems.Add(new ToolStripSeparator());
		int[] array4 = new int[6] { 100, 80, 60, 40, 20, 0 };
		foreach (int num4 in array4)
		{
			if (num4 == 0)
			{
				continue;
			}
			int fv = num4;
			volumeItem.DropDownItems.Add(fv + "%", null, (object param0, EventArgs param1) =>
			{
				SetVolume(fv);
			});
		}
		toolStripMenuItem.DropDownItems.Add(volumeItem);
		toolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
		autoRunItem = new ToolStripMenuItem("开机自启动", null, (object param0, EventArgs param1) =>
		{
			SetAutoRun(!settings.AutoRun);
		});
		toolStripMenuItem.DropDownItems.Add(autoRunItem);
		fullscreenItem = new ToolStripMenuItem("全屏时自动隐藏", null, (object param0, EventArgs param1) =>
		{
			SetAutoHide(!settings.AutoHide);
		});
		toolStripMenuItem.DropDownItems.Add(fullscreenItem);
		toolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
		toolStripMenuItem.DropDownItems.Add("退出奶蛙", null, (object param0, EventArgs param1) =>
		{
			Close();
		});
		contextMenuStrip2.Items.Add(toolStripMenuItem);
		contextMenuStrip2.Opening += (object param0, CancelEventArgs param1) =>
		{
			menuOpen = true;
			UpdateMenu();
		};
		contextMenuStrip2.Opened += (object param0, EventArgs param1) =>
		{
			menuOpenedAt = Now;
			menuMouseHoverAt = Now;
		};
		contextMenuStrip2.Closed += (object param0, ToolStripDropDownClosedEventArgs param1) =>
		{
			menuOpen = false;
			wasMenuLButton = false;
		};
		return contextMenuStrip2;
	}

	private void DemoAction(string name)
	{
		double now = Now;
		switch (name)
		{
		case "laugh":
			Laugh();
			return;
		case "idle":
			Motion.StartIdle(now, extraRandom.Next(4), 1400);
			break;
		case "idle5":
			Motion.StartIdle(now, 5, 16000);
			break;
		case "idle6":
			Motion.StartIdle(now, 6, 2000);
			break;
		case "walk":
			Motion.StartWalk(now, 1);
			break;
		case "seq1":
			Motion.StartHeadSeq(now);
			break;
		case "seq2":
			Motion.StartHeadSeq(now);
			Motion.StartHeadSeq(now);
			break;
		case "seq3":
			Motion.StartHeadSeq(now);
			Motion.StartHeadSeq(now);
			Motion.StartHeadSeq(now);
			break;
		case "dance":
			Motion.StartDance(now);
			break;
		case "hungry":
			Motion.StartHungry(now);
			bubbleText = "我好饿…";
			bubbleUntil = now + 10.0;
			break;
		case "fat":
			Motion.StartFat(now);
			fatBubbleShown = false;
			break;
		case "sleep":
			if (!Motion.Sleep(now))
			{
				Motion.Sleep(now);
			}
			break;
		}
		RenderNow(force: true);
	}

	private void UpdateMenu()
	{
		if (menu == null)
		{
			return;
		}
		SetToggleMark(autoRunItem, settings.AutoRun);
		SetToggleMark(fullscreenItem, settings.AutoHide);
		if (soundItem != null)
		{
			SetToggleMark(soundItem, settings.Sound);
		}
		if (volumeItem != null)
		{
			volumeItem.Text = "音量：" + settings.Volume + "%";
		}
		if (volumeMuteItem != null)
		{
			volumeMuteItem.Text = ((settings.Volume <= 0) ? "取消静音" : "静音");
		}
		if (hungerItem != null)
		{
			string text = "";
			for (int i = 0; i < 10; i++)
			{
				text += ((i < settings.Hunger) ? "▓" : "░");
			}
			hungerItem.Text = "饥饿 " + text + " " + settings.Hunger + "/10";
			hungerItem.Enabled = false;
		}
		foreach (ToolStripItem item in menu.Items)
		{
			if (!(item is ToolStripMenuItem toolStripMenuItem))
			{
				continue;
			}
			foreach (ToolStripItem dropDownItem in toolStripMenuItem.DropDownItems)
			{
				if (dropDownItem is ToolStripMenuItem toolStripMenuItem2 && toolStripMenuItem2.Tag is int)
				{
					toolStripMenuItem2.Checked = (int)toolStripMenuItem2.Tag == settings.Scale;
				}
			}
		}
	}

	private static void SetToggleMark(ToolStripMenuItem item, bool selected)
	{
		item.Checked = selected;
		item.ShowShortcutKeys = true;
		item.ShortcutKeyDisplayString = (selected ? "✓" : " ");
	}

	private bool MouseOnMenuTree()
	{
		if (menu.Bounds.Contains(Cursor.Position))
		{
			return true;
		}
		foreach (ToolStripItem item in menu.Items)
		{
			if (!(item is ToolStripMenuItem toolStripMenuItem))
			{
				continue;
			}
			if (toolStripMenuItem.DropDown.Visible && toolStripMenuItem.DropDown.Bounds.Contains(Cursor.Position))
			{
				return true;
			}
			foreach (ToolStripItem dropDownItem in toolStripMenuItem.DropDownItems)
			{
				if (dropDownItem is ToolStripMenuItem toolStripMenuItem2 && toolStripMenuItem2.DropDown.Visible && toolStripMenuItem2.DropDown.Bounds.Contains(Cursor.Position))
				{
					return true;
				}
			}
		}
		return false;
	}

	internal void SetSound(bool enabled)
	{
		settings.Sound = enabled;
		if (!settings.Sound)
		{
			try
			{
				if (laughSound != null)
				{
					laughSound.controls.stop();
					laughSound.URL = "";
				}
			}
			catch
			{
			}
			try
			{
				if (danceSound != null)
				{
					danceSound.controls.stop();
					danceSound.URL = "";
					danceFading = false;
				}
			}
			catch
			{
			}
			try
			{
				if (crySound != null)
				{
					crySound.controls.stop();
					crySound.URL = "";
				}
			}
			catch
			{
			}
		}
		UpdateMenu();
		Save();
	}

	internal void SetVolume(int v)
	{
		settings.Volume = Math.Max(0, Math.Min(100, v));
		if (settings.Volume > 0)
		{
			volumeMuteBack = settings.Volume;
		}
		try
		{
			if (volumeBarControl != null && volumeBarControl.Value != settings.Volume)
			{
				volumeBarControl.Value = settings.Volume;
			}
		}
		catch
		{
		}
		try
		{
			if (laughSound != null)
			{
				laughSound.settings.volume = settings.Volume;
			}
		}
		catch
		{
		}
		try
		{
			if (danceSound != null && !danceFading)
			{
				danceSound.settings.volume = settings.Volume;
			}
		}
		catch
		{
		}
		try
		{
			if (crySound != null)
			{
				crySound.settings.volume = settings.Volume;
			}
		}
		catch
		{
		}
		UpdateMenu();
		Save();
	}

	private void ToggleMute()
	{
		if (settings.Volume > 0)
		{
			volumeMuteBack = settings.Volume;
			SetVolume(0);
		}
		else
		{
			SetVolume((volumeMuteBack > 0) ? volumeMuteBack : 100);
		}
	}

	private dynamic EnsureLaughPlayer()
	{
		if (laughSound != null)
		{
			return laughSound;
		}
		try
		{
			Type typeFromProgID = Type.GetTypeFromProgID("WMPlayer.OCX");
			if (typeFromProgID == null)
			{
				LogAudio("EnsureLaughPlayer: WMPlayer.OCX 未注册");
				return null;
			}
			laughSound = Activator.CreateInstance(typeFromProgID);
			laughSound.uiMode = "none";
			laughSound.settings.volume = settings.Volume;
			laughSound.URL = assets.AudioPath;
			LogAudio("EnsureLaughPlayer: 已预载大笑音效");
		}
		catch (Exception ex)
		{
			LogAudio("EnsureLaughPlayer 异常: " + ex.Message);
		}
		return laughSound;
	}

	private dynamic EnsureCryPlayer()
	{
		if (crySound != null)
		{
			return crySound;
		}
		try
		{
			Type typeFromProgID = Type.GetTypeFromProgID("WMPlayer.OCX");
			if (typeFromProgID == null)
			{
				return null;
			}
			crySound = Activator.CreateInstance(typeFromProgID);
			crySound.uiMode = "none";
			crySound.settings.volume = settings.Volume;
			crySound.URL = CryAudio;
		}
		catch
		{
		}
		return crySound;
	}

	internal void PlayCry()
	{
		try
		{
			if (!settings.Sound || testing)
			{
				return;
			}
			if (!File.Exists(CryAudio))
			{
				return;
			}
			dynamic val = EnsureCryPlayer();
			if (val != null)
			{
				val.settings.volume = settings.Volume;
				val.controls.play();
			}
		}
		catch
		{
		}
	}

	internal static void LogAudio(string msg)
	{
		try
		{
			File.AppendAllText(Path.Combine(Settings.Folder, "audio.log"), DateTime.Now.ToString("s") + " " + msg + Environment.NewLine);
		}
		catch
		{
		}
	}

	private void StartDanceSound()
	{
		try
		{
			if (!File.Exists(DanceAudio))
			{
				LogAudio("StartDanceSound: 无 dance.mp3");
				return;
			}
			if (!settings.Sound)
			{
				LogAudio("StartDanceSound: 声音开关关");
				return;
			}
			if (danceSound == null)
			{
				Type typeFromProgID = Type.GetTypeFromProgID("WMPlayer.OCX");
				if (typeFromProgID == null)
				{
					LogAudio("StartDanceSound: WMPlayer.OCX 未注册");
					return;
				}
				danceSound = Activator.CreateInstance(typeFromProgID);
			}
			danceSound.uiMode = "none";
			danceSound.settings.volume = settings.Volume;
			danceSound.URL = DanceAudio;
			danceSound.controls.play();
			danceFading = false;
			LogAudio("StartDanceSound: 已开始播放");
		}
		catch (Exception ex)
		{
			LogAudio("StartDanceSound 异常: " + ex.Message);
		}
	}

	internal void StartCrowdSound()
	{
		LogAudio("StartCrowdSound 被调用, crowdSoundStarted=" + crowdSoundStarted);
		if (!crowdSoundStarted)
		{
			crowdSoundStarted = true;
			LogAudio("StartCrowdSound: 触发 settings.Sound=" + settings.Sound);
			if (settings.Sound)
			{
				StartDanceSound();
			}
		}
	}

	private void UpdateDanceSound()
	{
		try
		{
			if (crowdWindows != null)
			{
				if (!crowdSoundStarted && crowdCreatedAt >= 0.0 && Now - crowdCreatedAt >= 0.1)
				{
					StartCrowdSound();
				}
				return;
			}
			if (settings.Sound && File.Exists(DanceAudio) && Motion.Dancing)
			{
				if (Motion.Clock - Motion.DanceStart >= 1.0 && (danceSound == null || danceFading))
				{
					StartDanceSound();
				}
				return;
			}
			if (danceSound != null && !danceFading)
			{
				danceFading = true;
				danceFadeStart = Now;
			}
			if (danceSound != null && danceFading)
			{
				double num = Now - danceFadeStart;
				if (num >= 1.0)
				{
					danceSound.controls.stop();
					danceSound.URL = "";
					danceFading = false;
				}
				else
				{
					danceSound.settings.volume = Math.Max(0, settings.Volume - (int)((double)settings.Volume * num));
				}
			}
		}
		catch
		{
		}
	}

	internal bool Laugh()
	{
		if (HiddenForFullscreen)
		{
			return false;
		}
		if (Motion.Playing)
		{
			return false;
		}
		if (settings.Sound && !testing)
		{
			dynamic val = EnsureLaughPlayer();
			if (val != null)
			{
				try
				{
					val.settings.volume = settings.Volume;
					val.controls.play();
				}
				catch (Exception ex)
				{
					LogAudio("Laugh 播放异常: " + ex.Message);
				}
			}
		}
		Motion.Laugh(Now);
		RenderNow(force: true);
		return true;
	}

	internal void StopLaugh()
	{
		Motion.Stop();
		try
		{
			if (laughSound != null)
			{
				laughSound.controls.stop();
			}
		}
		catch
		{
		}
		RenderNow(force: true);
	}

	internal void Tick()
	{
		TickNow();
	}

	internal void TickNow()
	{
		TickWithForeground(Native.GetForegroundWindow());
	}

	internal void TickWithForeground(IntPtr foreground)
	{
		if (Now >= nextFullscreenCheck)
		{
			nextFullscreenCheck = Now + 0.12;
			CheckFullscreenWindow(foreground, immediate: false);
		}
		if (menuOpen && menu.Visible)
		{
			if (MouseOnMenuTree())
			{
				menuMouseHoverAt = Now;
				wasMenuLButton = false;
			}
			else if (Now - menuMouseHoverAt >= 3.0)
			{
				menu.Close();
			}
			else
			{
				bool flag = (Native.GetAsyncKeyState(1) & 0x8000) != 0;
				if (flag && !wasMenuLButton)
				{
					menu.Close();
				}
				wasMenuLButton = flag;
			}
		}
		if (HiddenForFullscreen)
		{
			return;
		}
		Point point = PointToClient(Cursor.Position);
		bool flag2 = !menuOpen && surface.Hit(point.X, point.Y);
		if (Motion.AutoLook)
		{
			double num = (double)Left + (double)Width / 2.0;
			double num2 = (double)Top + (double)PetHeight / 2.0;
			double num3 = (double)Cursor.Position.X - num;
			double num4 = (double)Cursor.Position.Y - num2;
			double val = Math.Max(40.0, Math.Sqrt(num3 * num3 + num4 * num4));
			Motion.LookGoal = Math.Max(0.0, Math.Min(1.0, 0.5 + num3 / (2.0 * Math.Max(180.0, val))));
			Motion.LookGoalV = Math.Max(0.0, Math.Min(1.0, 0.5 - num4 / (2.0 * Math.Max(180.0, val))));
		}
		if (bubbleText != null && Now >= bubbleUntil)
		{
			bubbleText = null;
			lastVisual = "";
		}
		if (flag2)
		{
			lastHover = Now;
			if (Motion.Sleeping)
			{
				Motion.Wake(Now);
			}
			if (Motion.Walking)
			{
				Motion.WalkStart = -1.0;
			}
		}
		else
		{
			if (!edgeRight && !Motion.Sleeping && !Motion.HasExtra && !Motion.Playing && !Motion.Resting && !Motion.Dragging && !Motion.HoverOpen && !Motion.Feeding && !gesture.Pressed && !menuOpen && Now >= nextIdle)
			{
				int num5 = extraRandom.Next(4);
				Motion.StartIdle(Now, num5, num5 switch
				{
					3 => 700, 
					0 => 1100, 
					_ => 900, 
				});
				nextIdle = Now + 45.0 + extraRandom.NextDouble() * 30.0;
			}
			else if (!edgeRight && !Motion.Sleeping && !Motion.HasExtra && !Motion.Playing && !Motion.Resting && !Motion.Dragging && !Motion.HoverOpen && !Motion.Feeding && !gesture.Pressed && !menuOpen && !gravityFalling && Now >= nextWalk)
			{
				Motion.StartWalk(Now, extraRandom.Next(2) * 2 - 1);
				nextWalk = Now + 30.0 + extraRandom.NextDouble() * 40.0;
			}
			// 待机大动作：无动静 60 秒随机触发（转手舞 / 睡觉），优先级最高，行走与弹跳中不触发
			if (!edgeRight && !Motion.Sleeping && !Motion.HasExtra && !Motion.Playing && !Motion.Resting && !Motion.Dragging && !Motion.HoverOpen && !Motion.Feeding && !Motion.Walking && !gravityFalling && !gesture.Pressed && !menuOpen && Now - lastHover > 60.0 && Now >= nextIdleRoll)
			{
				if (extraRandom.NextDouble() < 0.5)
				{
					Motion.StartIdle(Now, 5, 16000);
				}
				else
				{
					Motion.Sleep(Now);
				}
				nextIdleRoll = Now + 60.0;
			}
		}
		bool playing = Motion.Playing;
		Motion.Update(Now, flag2, assets.Duration, (double)assets.BlinkCount / assets.BlinkFps);
		if (playing && !Motion.Playing)
		{
			try
			{
				if (laughSound != null)
				{
					laughSound.controls.stop();
				}
			}
			catch
			{
			}
		}
		UpdateDanceSound();
		if (Motion.Fat && !fatBubbleShown && Motion.Clock - Motion.FatStart >= 1.2)
		{
			fatBubbleShown = true;
			ShowBubble("我的肚子真是肥嘟嘟的", 5.0);
		}
		long num6 = (long)(DateTimeOffset.UtcNow - UnixEpoch).TotalSeconds;
		if (settings.Hunger > 0 && num6 >= settings.HungerStamp)
		{
			settings.Hunger--;
			settings.HungerStamp = num6 + 600;
			if (settings.Hunger < 3)
			{
				Motion.StartHungry(Now);
				bubbleText = "我好饿…";
				bubbleUntil = Now + 10.0;
			}
			Save();
			UpdateMenu();
		}
		bool flag3 = Motion.HeadSequencing && Motion.HeadSeq == 3 && Motion.Clock - Motion.HeadSeqStart >= 1.0;
		if (flag3 && crowdWindows == null)
		{
			Point point2 = PointToScreen(new Point(Width / 2, 58 + PetHeight / 2));
			Rectangle workingArea = Screen.FromPoint(point2).WorkingArea;
			crowdWindows = new List<CrowdWindow>();
			crowdSoundStarted = false;
			crowdCreatedAt = Now;
			for (int i = 0; i < 6; i++)
			{
				crowdWindows.Add(new CrowdWindow(assets, i, point2, workingArea.Bottom, settings.Scale, this));
			}
			Visible = false;
		}
		if (!flag3 && crowdWindows != null)
		{
			foreach (CrowdWindow crowdWindow in crowdWindows)
			{
				crowdWindow.Close();
			}
			crowdWindows = null;
			if (danceSound != null)
			{
				try
				{
					danceSound.controls.stop();
					danceSound.URL = "";
					danceFading = false;
				}
				catch
				{
				}
			}
			Visible = true;
			RenderNow(force: true);
		}
		UpdateGravity();
		UpdateWalkMove();
		if (Motion.LandedWalkAt >= 0.0 && Motion.Walking && Now - Motion.LandedWalkAt >= 3.0)
		{
			Motion.LandedWalkAt = -1.0;
			Motion.StartIdle(Now, 6, 2000);
			PlayCry();
		}
		if (!edgeRight)
		{
			int num7 = assets.Width * settings.Scale / 100;
			if (Motion.Sleeping)
			{
				if (assets.Rest.Ready && assets.Rest.Width > assets.Width)
				{
					num7 = assets.Rest.Width * settings.Scale / 100;
				}
				if (assets.Rest2.Ready && assets.Rest2.Width > assets.Width)
				{
					num7 = assets.Rest2.Width * settings.Scale / 100;
				}
			}
			if (Width != num7)
			{
				int num8 = Left + Width / 2;
				Width = num7;
				Location = new Point(num8 - Width / 2, Location.Y);
				lastVisual = "";
			}
		}
		FinishDragIfDone();
		if (settings.ReminderMinutes > 0 && reminderNext >= 0.0 && Now >= reminderNext)
		{
			reminderNext = Now + (double)(settings.ReminderMinutes * 60);
			if (Motion.Sleeping)
			{
				Motion.Wake(Now);
			}
			Motion.Celebrate(Now);
			ShowBubble("该休息啦，起来活动一下！", 8.0);
			if (!settings.Sound)
			{
				try
				{
					SystemSounds.Asterisk.Play();
				}
				catch
				{
				}
			}
		}
		if (settings.Chime)
		{
			int hour = DateTime.Now.Hour;
			if (hour != lastChimeHour && DateTime.Now.Minute == 0 && DateTime.Now.Second < 4)
			{
				lastChimeHour = hour;
				Motion.Celebrate(Now);
				ShowBubble("现在 " + DateTime.Now.ToString("HH:mm") + " 啦", 5.0);
			}
		}
		int num9;
		if (HiddenForFullscreen)
		{
			num9 = 150;
		}
		else if (Motion.HoverOpen || Motion.Playing || Motion.Holding || Motion.Poking || Motion.HasExtra || gravityFalling || bubbleText != null || danceFading)
		{
			num9 = 16;
		}
		else if (Motion.Sleeping && assets.Rest.Ready && Motion.Clock - Motion.SleepStart < (double)assets.Rest.Count / assets.Rest.Fps)
		{
			num9 = 16;
		}
		else
		{
			num9 = (Motion.Sleeping ? 300 : 100);
		}
		if (timer.Interval != num9)
		{
			timer.Interval = num9;
		}
		RenderNow(force: false);
	}

	internal void FinishDragIfDone()
	{
		if (Motion.Dragging && Motion.Clock - Motion.DragStart >= Motion.DragDuration)
		{
			Motion.DragStart = -1.0;
		}
	}

	private void EatDown()
	{
		if (settings.Hunger > 0)
		{
			settings.Hunger--;
		}
		if (settings.Hunger < 3 && !Motion.Hungry)
		{
			Motion.StartHungry(Now);
			bubbleText = "我好饿…";
			bubbleUntil = Now + 10.0;
		}
		Save();
		UpdateMenu();
		RenderNow(force: true);
	}

	private void UpdateGravity()
	{
		if (!gravityFalling)
		{
			return;
		}
		double num = Math.Min(0.1, Math.Max(0.0, Now - lastGravityTick));
		lastGravityTick = Now;
		gravityVy += 1500.0 * num;
		Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
		double num2 = workingArea.Bottom - Height;
		double num3 = (double)Location.Y + gravityVy * num;
		if (num3 <= (double)workingArea.Top)
		{
			num3 = workingArea.Top;
			gravityVy = Math.Abs(gravityVy) * 0.35;
		}
		if (num3 >= num2)
		{
			num3 = num2;
			gravityVy *= -0.45;
			if (Math.Abs(gravityVy) < 50.0)
			{
				gravityFalling = false;
				Motion.Falling = false;
				Motion.LandingStart = Now;
				Motion.Start = -1.0;
				Motion.StartWalkLanded(Now, extraRandom.Next(2) * 2 - 1);
			}
			else
			{
				Motion.LandingStart = Now;
			}
		}
		Location = new Point(Location.X, (int)Math.Round(num3));
		lastVisual = "";
	}

	private void UpdateWalkMove()
	{
		if (Motion.Walking && !gravityFalling && !edgeRight)
		{
			double num = Math.Min(0.1, Math.Max(0.0, Now - lastWalkTick));
			lastWalkTick = Now;
			int num2 = Location.X + (int)Math.Round(120.0 * num * (double)Motion.WalkDir);
			Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
			if (num2 <= workingArea.Left)
			{
				num2 = workingArea.Left;
				Motion.WalkDir = 1;
			}
			else if (num2 + Width >= workingArea.Right)
			{
				num2 = workingArea.Right - Width;
				Motion.WalkDir = -1;
			}
			Location = new Point(num2, Location.Y);
			lastVisual = "";
		}
	}

	private void ShowBubble(string text, double seconds)
	{
		bubbleText = text;
		bubbleUntil = Now + seconds;
		RenderNow(force: true);
	}

	internal void SimulateHoverEnter()
	{
		OnHoverEnter();
	}

	internal void SimulateHoverLeave()
	{
		OnHoverLeave();
	}

	private bool PixelHit(DragEventArgs e)
	{
		Point point = PointToClient(new Point(e.X, e.Y));
		return SadHit(point.X, point.Y);
	}

	private void EnsureSadMask()
	{
		if (sadMask != null)
		{
			return;
		}
		try
		{
			assets.Feed.Ensure(assets);
			if (!assets.Feed.Ready)
			{
				return;
			}
			Bitmap bitmap = assets.Feed.Frame();
			if (bitmap == null)
			{
				return;
			}
			sadW = bitmap.Width;
			sadH = bitmap.Height;
			sadMask = new byte[sadW * sadH];
			BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, sadW, sadH), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			try
			{
				byte[] array = new byte[sadW * sadH * 4];
				Marshal.Copy(bitmapData.Scan0, array, 0, array.Length);
				for (int i = 0; i < sadW * sadH; i++)
				{
					sadMask[i] = array[i * 4 + 3];
				}
			}
			finally
			{
				bitmap.UnlockBits(bitmapData);
			}
		}
		catch
		{
		}
	}

	private bool SadHit(int x, int y)
	{
		EnsureSadMask();
		if (sadMask == null)
		{
			return false;
		}
		int num = assets.Width * settings.Scale / 100;
		int num2 = assets.Height * settings.Scale / 100;
		if (num <= 0 || num2 <= 0)
		{
			return false;
		}
		int num3 = (Width - num) / 2;
		int num4 = Height - num2;
		int num5 = (x - num3) * sadW / num;
		int num6 = (y - num4) * sadH / num2;
		if (num5 < 0 || num6 < 0 || num5 >= sadW || num6 >= sadH)
		{
			return false;
		}
		return sadMask[num6 * sadW + num5] > 24;
	}

	private void OnHoverEnter()
	{
		if (!edgeRight && !Motion.HoverOpen)
		{
			if (Motion.Dragging)
			{
				Motion.DragStart = -1.0;
			}
			if (Motion.Walking)
			{
				Motion.WalkStart = -1.0;
			}
			if (Motion.Dancing)
			{
				Motion.EndDance();
				danceInterrupted = true;
			}
			if (Motion.Sleeping)
			{
				Motion.Wake(Now);
			}
			Motion.HoverOpen = true;
			Motion.HoverStart = Now;
			Motion.FeedStart = -1.0;
			RenderNow(force: true);
		}
	}

	private void OnHoverLeave()
	{
		if (Motion.HoverOpen)
		{
			Motion.HoverOpen = false;
			danceInterrupted = false;
			if (!Motion.Dragging && !Motion.Walking && !gravityFalling)
			{
				Motion.ClearEffects(Now);
				Motion.FeedStart = Now;
			}
			RenderNow(force: true);
		}
	}

	internal void OnFileDrop(object sender, DragEventArgs e)
	{
		if (!e.Data.GetDataPresent(DataFormats.FileDrop) || edgeRight)
		{
			return;
		}
		string[] array = (string[])e.Data.GetData(DataFormats.FileDrop);
		if (array == null || array.Length == 0)
		{
			return;
		}
		if (Motion.Sleeping)
		{
			Motion.Wake(Now);
		}
		Motion.StartDrag(Now, (assets.Drag != null) ? assets.Drag.Duration : 6.0);
		int num = (settings.Recycle ? RecycleFiles(array) : 0);
		if (num > 0)
		{
			_ = settings.Hunger;
			if (settings.Hunger < 10)
			{
				settings.Hunger++;
			}
			settings.HungerStamp = (long)(DateTimeOffset.UtcNow - UnixEpoch).TotalSeconds + 600;
			if (Motion.Hungry)
			{
				Motion.EndHungry();
			}
			if (settings.Hunger >= 10)
			{
				Motion.DragDuration = Math.Min(Motion.DragDuration, 1.4);
				Motion.StartFat(Now);
				fatBubbleShown = false;
			}
			else
			{
				if (!danceInterrupted)
				{
					Motion.StartDance(Now);
				}
				ShowBubble("谢谢你！！", 4.0);
			}
			danceInterrupted = false;
			Save();
			UpdateMenu();
		}
		else if (settings.Recycle)
		{
			ShowBubble("回收失败（文件被占用？）已保留", 4.0);
		}
		else
		{
			ShowBubble("收到 " + array.Length + " 个文件（回收已关闭）", 3.0);
		}
	}

	internal void FeedFromCrowd(DragEventArgs e)
	{
		if (crowdWindows != null)
		{
			foreach (CrowdWindow crowdWindow in crowdWindows)
			{
				crowdWindow.Close();
			}
			crowdWindows = null;
		}
		Motion.EndHeadSeq();
		Visible = true;
		OnHoverEnter();
		OnFileDrop(this, e);
		RenderNow(force: true);
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	private static extern int SHFileOperation(ref SHFILEOPSTRUCT op);

	private static int RecycleFiles(string[] files)
	{
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (string value in files)
			{
				stringBuilder.Append(value);
				stringBuilder.Append('\0');
			}
			stringBuilder.Append('\0');
			SHFILEOPSTRUCT op = new SHFILEOPSTRUCT
			{
				wFunc = 3u,
				pFrom = stringBuilder.ToString(),
				fFlags = 592
			};
			return (SHFileOperation(ref op) == 0) ? files.Length : 0;
		}
		catch
		{
			return 0;
		}
	}

	internal void RenderNow(bool force)
	{
		if (!IsHandleCreated || disposing || HiddenForFullscreen || crowdWindows != null)
		{
			return;
		}
		string text = "0";
		if (Motion.Sleeping && assets.Rest.Ready)
		{
			double num = Motion.Clock - Motion.SleepStart;
			text = ((num < (double)assets.Rest.Count / assets.Rest.Fps) ? ("a" + Math.Min(assets.Rest.Count - 1, (int)(num * assets.Rest.Fps))) : "z");
		}
		string text2;
		if (Motion.Dragging)
		{
			text2 = "D" + (int)((Motion.Clock - Motion.DragStart) * assets.Drag.Fps);
		}
		else if (Motion.HoverOpen)
		{
			text2 = "H" + Math.Min(18, (int)((Motion.Clock - Motion.HoverStart) * 15.0));
		}
		else if (Motion.Feeding)
		{
			text2 = "F";
		}
		else if (Motion.Resting)
		{
			text2 = "R";
		}
		else if (Motion.Playing)
		{
			text2 = "L" + (int)(Motion.Elapsed * assets.Fps);
		}
		else if (Motion.Sleeping)
		{
			text2 = "S" + text;
		}
		else
		{
			text2 = (Motion.Blinking ? ("B" + ((Motion.Look > 0.5) ? 1 : 0) + "/" + (int)((Motion.Clock - Motion.BlinkStart) * assets.BlinkFps)) : ("E" + (int)Math.Round(Motion.Look * (double)(assets.EyeCount - 1)) + "/V" + (int)Math.Round(Motion.LookV * 30.0)));
		}
		if (Motion.Poking || Motion.Moving || Motion.Waking || Motion.Idling || Motion.HeadPoking || Motion.Celebrating || Motion.Landing || Motion.Walking || Motion.Falling)
		{
			text2 = text2 + "/M" + (int)(Motion.Clock * 60.0);
		}
		object obj = text2;
		text2 = string.Concat(obj, "/", settings.Scale, "/", settings.Alpha, settings.Mirror ? "/F" : "");
		if (force || !(text2 == lastVisual))
		{
			string bubble;
			if (bubbleText != null && Now < bubbleUntil)
			{
				bubble = bubbleText;
			}
			else
			{
				bubble = (Motion.Sleeping ? "z Z Z" : null);
			}
			using (Bitmap image = Renderer.Render(assets, Motion, settings.Scale, settings.Mirror, settings.Alpha, bubble, edgeRight))
			{
				surface.Update(Handle, image, Location, settings.Alpha * 255 / 100);
			}
			lastVisual = text2;
		}
	}

	internal void SetAutoHide(bool value)
	{
		settings.AutoHide = value;
		fullscreenGate.Reset();
		if (!value)
		{
			ApplyFullscreen(hidden: false);
		}
		else if (!testing)
		{
			CheckFullscreen(immediate: true);
		}
		Save();
		UpdateMenu();
	}

	internal void SetAlpha(int value)
	{
		settings.Alpha = Math.Max(40, Math.Min(100, value));
		RenderNow(force: true);
		Save();
		UpdateMenu();
	}

	internal void SetMirror(bool value)
	{
		settings.Mirror = value;
		RenderNow(force: true);
		Save();
		UpdateMenu();
	}

	internal void SetReminder(int minutes)
	{
		settings.ReminderMinutes = Math.Max(0, Math.Min(240, minutes));
		reminderNext = ((settings.ReminderMinutes > 0) ? (Now + (double)(settings.ReminderMinutes * 60)) : (-1.0));
		Save();
		UpdateMenu();
	}

	internal void SetChime(bool value)
	{
		settings.Chime = value;
		Save();
		UpdateMenu();
	}

	internal void SetAutoRun(bool value)
	{
		settings.AutoRun = value;
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true);
			if (registryKey == null)
			{
				return;
			}
			if (value)
			{
				registryKey.SetValue("NaiwaDesktopPet", "\"" + Application.ExecutablePath + "\"");
			}
			else
			{
				registryKey.DeleteValue("NaiwaDesktopPet", throwOnMissingValue: false);
			}
		}
		catch
		{
		}
		Save();
		UpdateMenu();
	}

	internal void SetEdge(bool value)
	{
		settings.Edge = value;
		edgeRight = false;
		if (value)
		{
			EdgeDock();
		}
		else
		{
			Home();
		}
		Save();
		UpdateMenu();
		RenderNow(force: true);
	}

	private int EdgeHeadPx()
	{
		return 117;
	}

	private int EdgeHeadWidth()
	{
		if (assets.Head.Width <= 0)
		{
			return 27;
		}
		return assets.Head.Width * 50 / 100;
	}

	private void EdgeDock()
	{
		Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
		Location = new Point(Math.Max(workingArea.Left, Math.Min(Left, workingArea.Right - Width)), workingArea.Bottom - EdgeHeadPx());
		KeepVisible();
	}

	private void SetEdgeRight(bool value)
	{
		edgeRight = value;
		if (value)
		{
			int num = ((assets.Head.Width > 0) ? (assets.Head.Width * settings.Scale / 100) : (assets.Width * settings.Scale / 100));
			int num2 = ((assets.Head.Height > 0) ? (assets.Head.Height * settings.Scale / 100) : (374 * settings.Scale / 100));
			ClientSize = new Size(num, num2);
			Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
			Location = new Point(workingArea.Right - num, Math.Max(workingArea.Top, Math.Min(Top, workingArea.Bottom - Height)));
		}
		else
		{
			ClientSize = new Size(SizeFor(settings.Scale).Width, SizeFor(settings.Scale).Height + 58);
		}
		RenderNow(force: true);
	}

	private void EdgeDockRight()
	{
		Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
		Location = new Point(workingArea.Right - Width, Math.Max(workingArea.Top, Math.Min(Location.Y, workingArea.Bottom - PetHeight)));
		KeepVisible();
	}

	private void CheckFullscreen(bool immediate)
	{
		CheckFullscreenWindow(Native.GetForegroundWindow(), immediate);
	}

	private void CheckFullscreenWindow(IntPtr foreground, bool immediate)
	{
		if (ready && !menuOpen && !gesture.Pressed)
		{
			bool flag = settings.AutoHide && Fullscreen.IsActive(foreground, Handle, Screen.FromRectangle(Bounds).Bounds);
			if (immediate)
			{
				fullscreenGate.Set(flag, Now);
				ApplyFullscreen(flag);
			}
			else if (fullscreenGate.Update(flag, Now))
			{
				ApplyFullscreen(fullscreenGate.Hidden);
			}
		}
	}

	internal void ApplyFullscreen(bool hidden)
	{
		if (hidden == HiddenForFullscreen)
		{
			return;
		}
		HiddenForFullscreen = hidden;
		if (hidden)
		{
			gesture.Cancel();
			Capture = false;
			Motion.Suspend(Now);
			try
			{
				if (laughSound != null)
				{
					laughSound.controls.stop();
				}
			}
			catch
			{
			}
			Hide();
			timer.Interval = 150;
		}
		else
		{
			Motion.Suspend(Now);
			timer.Interval = 16;
			Show();
			RenderNow(force: true);
		}
	}

	private void ReturnHome()
	{
		if (edgeRight)
		{
			SetEdgeRight(value: false);
		}
		if (settings.Edge)
		{
			settings.Edge = false;
			UpdateMenu();
		}
		Home();
		if (!testing)
		{
			CheckFullscreen(immediate: true);
		}
		if (!HiddenForFullscreen)
		{
			Show();
			RenderNow(force: true);
		}
		Save();
	}

	private Size SizeFor(int scale)
	{
		int num = assets.Height;
		if (assets.Drag.Height > num)
		{
			num = assets.Drag.Height;
		}
		if (assets.Feed.Height > num)
		{
			num = assets.Feed.Height;
		}
		return new Size(assets.Width * scale / 100, num * scale / 100);
	}

	internal void SetSize(int scale)
	{
		int num = Left + Width / 2;
		int num2 = Top + PetHeight;
		settings.Scale = Math.Max(50, Math.Min(150, scale));
		ClientSize = new Size(SizeFor(settings.Scale).Width, SizeFor(settings.Scale).Height + 58);
		Location = new Point(num - Width / 2, num2 - PetHeight);
		KeepVisible();
		RenderNow(force: true);
		Save();
		UpdateMenu();
	}

	internal void Home()
	{
		Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
		Location = new Point(workingArea.Right - Width - 24, workingArea.Bottom - PetHeight + (int)((double)(12 * settings.Scale) / 100.0));
		KeepVisible();
	}

	internal static Point Clamp(Point p, Size size, Rectangle area, int allowance)
	{
		return new Point(Math.Max(area.Left, Math.Min(p.X, area.Right - size.Width)), Math.Max(area.Top, Math.Min(p.Y, area.Bottom - size.Height + allowance)));
	}

	private void KeepVisible()
	{
		if (gravityFalling)
		{
			gravityFalling = false;
			Motion.Falling = false;
		}
		Rectangle workingArea = Screen.FromRectangle(Bounds).WorkingArea;
		if (settings.Edge)
		{
			Location = new Point(Math.Max(workingArea.Left, Math.Min(Location.X, workingArea.Right - Width)), workingArea.Bottom - EdgeHeadPx());
		}
		else if (edgeRight)
		{
			Location = new Point(workingArea.Right - Width, Math.Max(workingArea.Top, Math.Min(Location.Y, workingArea.Bottom - Height)));
		}
		else
		{
			Location = Clamp(Location, new Size(Width, PetHeight), workingArea, (int)((double)(12 * settings.Scale) / 100.0));
		}
	}

	private void Save()
	{
		if (!testing)
		{
			settings.X = Left;
			settings.Y = Top;
			settings.Save();
		}
	}

	private double PetX(int winX)
	{
		double num = (double)winX * 100.0 / (double)settings.Scale;
		if (!settings.Mirror)
		{
			return num;
		}
		return (double)assets.Width - num;
	}

	private double PetY(int winY)
	{
		return (double)(winY - 58) * 100.0 / (double)settings.Scale;
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		if (e.Button == MouseButtons.Right)
		{
			Motion.EndDrag(Now);
			gesture.Cancel();
			Capture = false;
			Rectangle workingArea = Screen.FromControl(this).WorkingArea;
			int num = Right + 4;
			int num2 = Top + 8;
			if (num + menu.Width > workingArea.Right)
			{
				num = Left - menu.Width - 4;
			}
			if (num2 + menu.Height > workingArea.Bottom)
			{
				num2 = workingArea.Bottom - menu.Height;
			}
			if (num2 < workingArea.Top)
			{
				num2 = workingArea.Top;
			}
			menu.Show(new Point(num, num2));
		}
		else if (e.Button == MouseButtons.Left)
		{
			if (edgeRight)
			{
				SetEdgeRight(value: false);
				KeepVisible();
			}
			if (settings.Edge)
			{
				settings.Edge = false;
				UpdateMenu();
			}
			gesture.Down(PointToScreen(e.Location));
			lastDragPointer = gesture.Origin;
			dragWindow = Location;
			Capture = true;
			gravityFalling = false;
			Motion.Falling = false;
		}
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		if (!gesture.Pressed)
		{
			return;
		}
		Point p = PointToScreen(e.Location);
		gesture.Move(p);
		if (!gesture.Dragged)
		{
			return;
		}
		if (!Motion.Holding)
		{
			Motion.BeginDrag(Now);
			try
			{
				if (laughSound != null)
				{
					laughSound.controls.stop();
				}
			}
			catch
			{
			}
		}
		Motion.Drag(Now, p.X - lastDragPointer.X);
		dragVy = (p.Y - lastDragPointer.Y) * 150;
		if (dragVy < dragVyPeak)
		{
			dragVyPeak = dragVy;
		}
		lastDragPointer = p;
		Location = new Point(dragWindow.X + p.X - gesture.Origin.X, dragWindow.Y + p.Y - gesture.Origin.Y);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		base.OnMouseUp(e);
		if (e.Button != MouseButtons.Left || !gesture.Pressed)
		{
			return;
		}
		bool dragged = gesture.Dragged;
		bool flag = gesture.Up(PointToScreen(e.Location), Now);
		Capture = false;
		if (dragged)
		{
			Motion.EndDrag(Now);
			Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
			if (!settings.Edge && Left + Width >= workingArea.Right - 24)
			{
				SetEdgeRight(value: true);
			}
			else
			{
				KeepVisible();
				gravityFalling = true;
				Motion.Falling = true;
				gravityVy = ((dragVyPeak < -120.0) ? Math.Max(-1000.0, dragVyPeak) : 0.0);
				dragVyPeak = 0.0;
				lastGravityTick = Now;
			}
			RenderNow(force: true);
			Save();
		}
		else
		{
			if (edgeRight)
			{
				return;
			}
			if (flag)
			{
				double num = PetX(e.X);
				double num2 = PetY(e.Y);
				if (Motion.HeadSequencing)
				{
					double num3 = (double)settings.Scale / 100.0;
					double num4 = ((double)Width - (double)assets.Seq.Width * num3) / 2.0;
					double num5 = (double)(58 + PetHeight) - (double)assets.Seq.Height * num3;
					double num6 = (double)assets.Seq.Width * num3;
					double num7 = (double)assets.Seq.Height * num3;
					if (!((double)e.X >= num4) || !((double)e.X < num4 + num6) || !((double)e.Y >= num5) || !((double)e.Y < num5 + num7))
					{
						return;
					}
					Bitmap bitmap = assets.Seq.Frame(Motion.HeadSeq - 1);
					if (bitmap != null)
					{
						int num8 = (int)(((double)e.X - num4) / num6 * (double)bitmap.Width);
						int num9 = (int)(((double)e.Y - num5) / num7 * (double)bitmap.Height);
						if (num8 >= 0 && num9 >= 0 && num8 < bitmap.Width && num9 < bitmap.Height && bitmap.GetPixel(num8, num9).A > 16)
						{
							Motion.StartHeadSeq(Now);
							RenderNow(force: true);
						}
					}
				}
				else if (Motion.InBelly(num, num2))
				{
					Laugh();
				}
				else if (Motion.InHead(num, num2))
				{
					Motion.StartHeadSeq(Now);
					RenderNow(force: true);
				}
			}
			else if (Motion.Resting)
			{
				Motion.Stand(Now);
				RenderNow(force: true);
			}
			else if (!Motion.HeadSequencing)
			{
				double num10 = PetX(e.X);
				double num11 = PetY(e.Y);
				if (Motion.Poke(Now, num10, num11))
				{
					RenderNow(force: true);
				}
				else if (Motion.InHead(num10, num11) && Motion.HeadPoke(Now))
				{
					RenderNow(force: true);
				}
			}
		}
	}

	protected override void OnMouseCaptureChanged(EventArgs e)
	{
		base.OnMouseCaptureChanged(e);
		if (!Capture && gesture.Pressed)
		{
			Motion.EndDrag(Now);
			gesture.Cancel();
			KeepVisible();
			RenderNow(force: true);
			Save();
		}
	}

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		Save();
		timer.Stop();
		try
		{
			if (laughSound != null)
			{
				laughSound.controls.stop();
			}
		}
		catch
		{
		}
		base.OnFormClosing(e);
	}

	protected override void Dispose(bool disposingNow)
	{
		if (disposingNow && !disposing)
		{
			disposing = true;
			timer.Dispose();
			if (crowdWindows != null)
			{
				foreach (CrowdWindow crowdWindow in crowdWindows)
				{
					crowdWindow.Close();
				}
				crowdWindows = null;
			}
			if (tray != null)
			{
				tray.Visible = false;
				tray.Dispose();
			}
			if (menu != null)
			{
				Font font = menu.Font;
				menu.Dispose();
				font.Dispose();
			}
			try
			{
				if (laughSound != null)
				{
					laughSound.controls.stop();
					laughSound.close();
				}
			}
			catch
			{
			}
			laughSound = null;
			if (danceSound != null)
			{
				try
				{
					danceSound.controls.stop();
					danceSound.close();
				}
				catch
				{
				}
				danceSound = null;
			}
			if (crySound != null)
			{
				try
				{
					crySound.controls.stop();
					crySound.close();
				}
				catch
				{
				}
				crySound = null;
			}
			surface.Dispose();
			if (petIcon != null)
			{
				petIcon.Dispose();
			}
		}
		base.Dispose(disposingNow);
	}
}
