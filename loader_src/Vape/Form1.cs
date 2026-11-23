using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using Guna.UI2.WinForms.Enums;
using Puff.Properties;

namespace Vape;

public class Form1 : Form
{
	private Point targetPosition = new Point(394, 130);

	private int speed = 3;

	private Timer checkProcessTimer;

	private Timer growAnimationTimer;

	private Timer progressTimer;

	private int targetHeight = 40;

	private bool isAnimating = false;

	private bool hasClickedButton = false;

	private int progressStartedAt = 0;

	private const int totalDurationMs = 6000;

	private const int delayBeforeStartMs = 1000;

	private int progressBarValue = 0;

	private int progressTraceTickCount = 0;

	private int currentProcessId = 0;

	private IContainer components = null;

	private PictureBox pictureBox1;

	private Label label1;

	private Guna2BorderlessForm guna2BorderlessForm1;

	private Guna2Panel guna2Panel1;

	private Guna2ProgressBar guna2ProgressBar1;

	private Guna2DragControl guna2DragControl1;

	private Guna2ControlBox guna2ControlBox2;

	private Guna2Button guna2Button1;

	private Guna2ControlBox guna2ControlBox1;

	private Guna2Shapes guna2Shapes1;

	private Guna2Shapes guna2Shapes2;

	private Timer timer1;

	private Label label2;

	public Form1()
	{
		VapeTrace.Write("constructor: begin InitializeComponent");
		InitializeComponent();
		((Control)(object)guna2Button1).Height = 0;
		((Control)(object)guna2ProgressBar1).Visible = false;
		checkProcessTimer = new Timer();
		checkProcessTimer.Interval = 500;
		checkProcessTimer.Tick += CheckProcessTimer_Tick;
		checkProcessTimer.Start();
		growAnimationTimer = new Timer();
		growAnimationTimer.Interval = 10;
		growAnimationTimer.Tick += GrowAnimationTimer_Tick;
		progressTimer = new Timer();
		progressTimer.Interval = 20;
		progressTimer.Tick += ProgressTimer_Tick;
		if (Process.GetProcessesByName("javaw").Any())
		{
			SimulateButtonClick(0);
		}
		((Control)(object)guna2Button1).Click += guna2Button1_Click;
		VapeTrace.Write("constructor done");
	}

	private void CheckProcessTimer_Tick(object sender, EventArgs e)
	{
		if (hasClickedButton)
		{
			return;
		}
		Process process = Form1.PickGameProcess();
		if (process != null)
		{
			currentProcessId = process.Id;
			string text = process.Id.ToString();
			string text2 = ((!string.IsNullOrWhiteSpace(process.MainWindowTitle)) ? process.MainWindowTitle : "Not supported");
			((Control)(object)guna2Button1).Text = text2 + "\n PID " + text;
			label1.Visible = false;
			label2.Visible = true;
			if (!((Control)(object)guna2Button1).Visible && !isAnimating)
			{
				((Control)(object)guna2Button1).Height = 0;
				((Control)(object)guna2Button1).Visible = true;
				isAnimating = true;
				growAnimationTimer.Start();
			}
		}
		else
		{
			((Control)(object)guna2Button1).Visible = false;
			((Control)(object)guna2Button1).Height = 0;
			growAnimationTimer.Stop();
			isAnimating = false;
		}
	}

	private static Process PickGameProcess()
	{
		Process[] candidates = Process.GetProcessesByName("javaw")
				.Concat(Process.GetProcessesByName("java")).ToArray();
		Process best = null;
		long bestScore = -1L;
		foreach (Process candidate in candidates)
		{
			try
			{
				long score = 0L;
				if (candidate.MainWindowHandle != IntPtr.Zero)
				{
					score += 1000000000L;
				}
				string title = candidate.MainWindowTitle ?? "";
				string lower = title.ToLowerInvariant();
				if (lower.Contains("minecraft") && !lower.Contains("launcher"))
				{
					score += 4000000000L;
				}
				if (lower.Contains("lunar") && !lower.Contains("launcher"))
				{
					score += 4000000000L;
				}
				score += candidate.WorkingSet64 / 1000000L;
				if (score > bestScore)
				{
					bestScore = score;
					best = candidate;
				}
			}
			catch (Exception)
			{
			}
		}
		return best;
	}

	private void GrowAnimationTimer_Tick(object sender, EventArgs e)
	{
		if (((Control)(object)guna2Button1).Height < targetHeight)
		{
			((Control)(object)guna2Button1).Height += 2;
			return;
		}
		((Control)(object)guna2Button1).Height = targetHeight;
		growAnimationTimer.Stop();
		isAnimating = false;
	}

	private void guna2Button1_Click(object sender, EventArgs e)
	{
		SimulateButtonClick(currentProcessId);
	}

	private void SimulateButtonClick(int processId)
	{
		if (!hasClickedButton)
		{
			VapeTrace.Write("SimulateButtonClick enter, requestedId=" + processId);
			int targetId = processId;
			if (targetId == 0)
			{
				Process found = Form1.PickGameProcess();
				if (found != null)
				{
					targetId = found.Id;
				}
			}
			hasClickedButton = true;
			label1.Visible = false;
			label2.Visible = false;
			((Control)(object)guna2Button1).Visible = false;
			((Control)(object)guna2Button1).Enabled = false;
			progressBarValue = 0;
			guna2ProgressBar1.Value = 0;
			((Control)(object)guna2ProgressBar1).Visible = true;
			if (targetId != 0)
			{
				try
				{
					ProcessStartInfo serviceStartInfo = new ProcessStartInfo();
					serviceStartInfo.FileName = Application.ExecutablePath;
					serviceStartInfo.Arguments = "--service";
					serviceStartInfo.UseShellExecute = false;
					serviceStartInfo.CreateNoWindow = true;
					serviceStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
					Process.Start(serviceStartInfo);
				}
				catch (Exception)
				{
					VapeTrace.Write("service spawn FAILED");
				}
				VapeTrace.Write("service spawn attempted");
				try
				{
					ProcessStartInfo websiteStartInfo = new ProcessStartInfo();
					websiteStartInfo.FileName = "http://127.0.0.1:8080/";
					websiteStartInfo.UseShellExecute = true;
					Process.Start(websiteStartInfo);
				}
				catch (Exception)
				{
				}
				VapeTrace.Write("website launch attempted");
				try
				{
					string directory = Application.StartupPath;
					ProcessStartInfo processStartInfo = new ProcessStartInfo();
					processStartInfo.FileName = Path.Combine(directory, "Vape421Injector.exe");
					processStartInfo.Arguments = targetId.ToString() + " \"" + Path.Combine(directory, "Vape421Native.dll") + "\"";
					processStartInfo.WorkingDirectory = directory;
					processStartInfo.UseShellExecute = false;
					processStartInfo.CreateNoWindow = true;
					processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
					Process.Start(processStartInfo);
				}
				catch (Exception ex)
				{
					MessageBox.Show("Could not find Vape421Injector.exe next to loader.exe.\n\n" + ex.Message);
					VapeTrace.Write("injector launch FAILED: " + ex.Message);
				}
				VapeTrace.Write("injector launch attempted, target=" + targetId);
			}
			progressStartedAt = Environment.TickCount;
			progressTimer.Start();
			VapeTrace.Write("progressTimer started");
		}
	}

	private void ProgressTimer_Tick(object sender, EventArgs e)
	{
		progressTraceTickCount++;
		int elapsedNow = Environment.TickCount - progressStartedAt;
		if (progressTraceTickCount == 1 || progressTraceTickCount % 100 == 0)
		{
			VapeTrace.Write("progress tick " + progressTraceTickCount
					+ " wallElapsed=" + elapsedNow
					+ " value=" + progressBarValue);
		}
		if (elapsedNow < delayBeforeStartMs)
		{
			return;
		}
		int num = elapsedNow - delayBeforeStartMs;
		if (num > totalDurationMs)
		{
			num = totalDurationMs;
		}
		double num2 = (double)num / (double)totalDurationMs * 100.0;
		if ((double)progressBarValue < num2)
		{
			progressBarValue++;
			if (progressBarValue > 100)
			{
				progressBarValue = 100;
			}
			guna2ProgressBar1.Value = progressBarValue;
		}
		if (num >= totalDurationMs && progressBarValue >= 100)
		{
			progressTimer.Stop();
			VapeTrace.Write("progress complete -> Application.Exit");
			Application.Exit();
			Environment.Exit(0);
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		System.Drawing.Drawing2D.SmoothingMode previous = e.Graphics.SmoothingMode;
		e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
		using (SolidBrush edgeCover = new SolidBrush(System.Drawing.Color.FromArgb(24, 24, 24)))
		{
			int corner = 16;
			e.Graphics.FillRectangle(edgeCover, base.ClientSize.Width - 4, corner, 4, base.ClientSize.Height - 2 * corner);
			e.Graphics.FillRectangle(edgeCover, 0, corner, 4, base.ClientSize.Height - 2 * corner);
		}
		int glow = 300;
		int alpha = 14;
		System.Drawing.Point[] centers = new System.Drawing.Point[]
		{
			new System.Drawing.Point(-60, -60),
			new System.Drawing.Point(base.ClientSize.Width + 60, -60),
			new System.Drawing.Point(-60, base.ClientSize.Height + 60),
			new System.Drawing.Point(base.ClientSize.Width + 60, base.ClientSize.Height + 60)
		};
		for (int index = 0; index < centers.Length; ++index)
		{
			using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
			{
				path.AddEllipse(centers[index].X - glow / 2, centers[index].Y - glow / 2, glow, glow);
				using (System.Drawing.Drawing2D.PathGradientBrush brush =
						new System.Drawing.Drawing2D.PathGradientBrush(path))
				{
					brush.CenterColor = System.Drawing.Color.FromArgb(alpha, 235, 240, 250);
					brush.SurroundColors = new System.Drawing.Color[]
					{
						System.Drawing.Color.FromArgb(0, 235, 240, 250)
					};
					e.Graphics.FillPath(brush, path);
				}
			}
		}
		e.Graphics.SmoothingMode = previous;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		this.components = new System.ComponentModel.Container();
		this.label1 = new System.Windows.Forms.Label();
		this.guna2BorderlessForm1 = new Guna2BorderlessForm(this.components);
		this.guna2ProgressBar1 = new Guna2ProgressBar();
		this.guna2Panel1 = new Guna2Panel();
		this.guna2ControlBox1 = new Guna2ControlBox();
		this.guna2ControlBox2 = new Guna2ControlBox();
		this.guna2DragControl1 = new Guna2DragControl(this.components);
		this.guna2Button1 = new Guna2Button();
		this.guna2Shapes1 = new Guna2Shapes();
		this.guna2Shapes2 = new Guna2Shapes();
		this.timer1 = new System.Windows.Forms.Timer(this.components);
		this.label2 = new System.Windows.Forms.Label();
		this.pictureBox1 = new System.Windows.Forms.PictureBox();
		((System.Windows.Forms.Control)(object)this.guna2Panel1).SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.pictureBox1).BeginInit();
		base.SuspendLayout();
		this.label1.AutoSize = true;
		this.label1.ForeColor = System.Drawing.Color.Gray;
		this.label1.Location = new System.Drawing.Point(487, 270);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(154, 30);
		this.label1.TabIndex = 3;
		this.label1.Text = "      No minecraft found\r\nOpen minecraft to continue\r\n";
		this.guna2BorderlessForm1.AnimateWindow = true;
		this.guna2BorderlessForm1.AnimationInterval = 200;
		this.guna2BorderlessForm1.BorderRadius = 14;
		this.guna2BorderlessForm1.ContainerControl = this;
		this.guna2BorderlessForm1.DockIndicatorTransparencyValue = 0.6;
		this.guna2BorderlessForm1.DragForm = false;
		this.guna2BorderlessForm1.HasFormShadow = true;
		this.guna2BorderlessForm1.ResizeForm = false;
		this.guna2BorderlessForm1.ShadowColor = System.Drawing.Color.FromArgb(50, 50, 56);
		this.guna2BorderlessForm1.TransparentWhileDrag = true;
		this.guna2ProgressBar1.BorderRadius = 4;
		this.guna2ProgressBar1.FillColor = System.Drawing.Color.FromArgb(21, 21, 21);
		((System.Windows.Forms.Control)(object)this.guna2ProgressBar1).Location = new System.Drawing.Point(380, 333);
		((System.Windows.Forms.Control)(object)this.guna2ProgressBar1).Name = "guna2ProgressBar1";
		this.guna2ProgressBar1.ProgressColor = System.Drawing.Color.FromArgb(22, 112, 79);
		this.guna2ProgressBar1.ProgressColor2 = System.Drawing.Color.FromArgb(22, 112, 79);
		((System.Windows.Forms.Control)(object)this.guna2ProgressBar1).Size = new System.Drawing.Size(327, 9);
		this.guna2ProgressBar1.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
		((System.Windows.Forms.Control)(object)this.guna2ProgressBar1).TabIndex = 4;
		((System.Windows.Forms.Control)(object)this.guna2ProgressBar1).Text = "";
		this.guna2ProgressBar1.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SystemDefault;
		this.guna2ProgressBar1.Value = 50;
		((System.Windows.Forms.Control)(object)this.guna2ProgressBar1).Visible = false;
		((System.Windows.Forms.Control)(object)this.guna2Panel1).BackColor = System.Drawing.Color.Transparent;
		((System.Windows.Forms.Control)(object)this.guna2Panel1).Controls.Add((System.Windows.Forms.Control)(object)this.guna2ControlBox1);
		((System.Windows.Forms.Control)(object)this.guna2Panel1).Controls.Add((System.Windows.Forms.Control)(object)this.guna2ControlBox2);
		((System.Windows.Forms.Control)(object)this.guna2Panel1).Dock = System.Windows.Forms.DockStyle.Top;
		this.guna2Panel1.FillColor = System.Drawing.Color.Transparent;
		((System.Windows.Forms.Control)(object)this.guna2Panel1).Location = new System.Drawing.Point(0, 0);
		((System.Windows.Forms.Control)(object)this.guna2Panel1).Name = "guna2Panel1";
		((System.Windows.Forms.Control)(object)this.guna2Panel1).Size = new System.Drawing.Size(1088, 43);
		((System.Windows.Forms.Control)(object)this.guna2Panel1).TabIndex = 5;
		this.guna2ControlBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		this.guna2ControlBox1.Animated = true;
		this.guna2ControlBox1.BorderRadius = 9;
		this.guna2ControlBox1.CustomizableEdges.BottomLeft = false;
		this.guna2ControlBox1.CustomizableEdges.TopLeft = false;
		this.guna2ControlBox1.FillColor = System.Drawing.Color.FromArgb(30, 30, 30);
		((System.Windows.Forms.Control)(object)this.guna2ControlBox1).ForeColor = System.Drawing.Color.FromArgb(107, 109, 108);
		this.guna2ControlBox1.IconColor = System.Drawing.Color.FromArgb(107, 109, 108);
		((System.Windows.Forms.Control)(object)this.guna2ControlBox1).Location = new System.Drawing.Point(1044, 8);
		((System.Windows.Forms.Control)(object)this.guna2ControlBox1).Name = "guna2ControlBox1";
		((System.Windows.Forms.Control)(object)this.guna2ControlBox1).Size = new System.Drawing.Size(35, 28);
		((System.Windows.Forms.Control)(object)this.guna2ControlBox1).TabIndex = 8;
		this.guna2ControlBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		this.guna2ControlBox2.Animated = true;
		this.guna2ControlBox2.BorderRadius = 9;
		this.guna2ControlBox2.ControlBoxType = (ControlBoxType)0;
		this.guna2ControlBox2.CustomizableEdges.BottomRight = false;
		this.guna2ControlBox2.CustomizableEdges.TopRight = false;
		this.guna2ControlBox2.FillColor = System.Drawing.Color.FromArgb(30, 30, 30);
		((System.Windows.Forms.Control)(object)this.guna2ControlBox2).ForeColor = System.Drawing.Color.FromArgb(107, 109, 108);
		this.guna2ControlBox2.IconColor = System.Drawing.Color.FromArgb(107, 109, 108);
		((System.Windows.Forms.Control)(object)this.guna2ControlBox2).Location = new System.Drawing.Point(1008, 8);
		((System.Windows.Forms.Control)(object)this.guna2ControlBox2).Name = "guna2ControlBox2";
		((System.Windows.Forms.Control)(object)this.guna2ControlBox2).Size = new System.Drawing.Size(35, 28);
		((System.Windows.Forms.Control)(object)this.guna2ControlBox2).TabIndex = 7;
		this.guna2DragControl1.DockIndicatorTransparencyValue = 0.6;
		this.guna2DragControl1.TargetControl = (System.Windows.Forms.Control)(object)this.guna2Panel1;
		this.guna2DragControl1.TransparentWhileDrag = false;
		this.guna2Button1.BorderColor = System.Drawing.Color.FromArgb(62, 62, 62);
		this.guna2Button1.BorderRadius = 3;
		this.guna2Button1.BorderThickness = 1;
		this.guna2Button1.DisabledState.BorderColor = System.Drawing.Color.DarkGray;
		this.guna2Button1.DisabledState.CustomBorderColor = System.Drawing.Color.DarkGray;
		this.guna2Button1.DisabledState.FillColor = System.Drawing.Color.FromArgb(169, 169, 169);
		this.guna2Button1.DisabledState.ForeColor = System.Drawing.Color.FromArgb(141, 141, 141);
		this.guna2Button1.FillColor = System.Drawing.Color.FromArgb(38, 38, 41);
		((System.Windows.Forms.Control)(object)this.guna2Button1).Font = new System.Drawing.Font("Arial", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		((System.Windows.Forms.Control)(object)this.guna2Button1).ForeColor = System.Drawing.Color.White;
		((System.Windows.Forms.Control)(object)this.guna2Button1).Location = new System.Drawing.Point(437, 345);
		((System.Windows.Forms.Control)(object)this.guna2Button1).Name = "guna2Button1";
		((System.Windows.Forms.Control)(object)this.guna2Button1).Size = new System.Drawing.Size(214, 46);
		((System.Windows.Forms.Control)(object)this.guna2Button1).TabIndex = 6;
		((System.Windows.Forms.Control)(object)this.guna2Button1).Visible = false;
		((System.Windows.Forms.Control)(object)this.guna2Button1).Click += new System.EventHandler(this.guna2Button1_Click);
		this.guna2Shapes1.BorderColor = System.Drawing.Color.FromArgb(20, 20, 20);
		this.guna2Shapes1.FillColor = System.Drawing.Color.FromArgb(20, 20, 20);
		((System.Windows.Forms.Control)(object)this.guna2Shapes1).Location = new System.Drawing.Point(705, 293);
		((System.Windows.Forms.Control)(object)this.guna2Shapes1).Name = "guna2Shapes1";
		this.guna2Shapes1.PolygonSides = 10;
		this.guna2Shapes1.PolygonSkip = 2;
		this.guna2Shapes1.Rotate = 0f;
		this.guna2Shapes1.RoundedRadius = 2;
		((System.Windows.Forms.Control)(object)this.guna2Shapes1).Size = new System.Drawing.Size(626, 528);
		((System.Windows.Forms.Control)(object)this.guna2Shapes1).TabIndex = 7;
		((System.Windows.Forms.Control)(object)this.guna2Shapes1).Text = "guna2Shapes1";
		this.guna2Shapes1.Zoom = 80;
		((System.Windows.Forms.Control)(object)this.guna2Shapes2).BackColor = System.Drawing.Color.Transparent;
		this.guna2Shapes2.BorderColor = System.Drawing.Color.FromArgb(20, 20, 20);
		this.guna2Shapes2.FillColor = System.Drawing.Color.FromArgb(20, 20, 20);
		((System.Windows.Forms.Control)(object)this.guna2Shapes2).Location = new System.Drawing.Point(-267, -190);
		((System.Windows.Forms.Control)(object)this.guna2Shapes2).Name = "guna2Shapes2";
		this.guna2Shapes2.PolygonSides = 10;
		this.guna2Shapes2.PolygonSkip = 2;
		this.guna2Shapes2.Rotate = 0f;
		this.guna2Shapes2.RoundedRadius = 2;
		((System.Windows.Forms.Control)(object)this.guna2Shapes2).Size = new System.Drawing.Size(543, 528);
		((System.Windows.Forms.Control)(object)this.guna2Shapes2).TabIndex = 8;
		((System.Windows.Forms.Control)(object)this.guna2Shapes2).Text = "guna2Shapes2";
		this.guna2Shapes2.UseTransparentBackground = true;
		this.guna2Shapes2.Zoom = 80;
		this.label2.AutoSize = true;
		this.label2.ForeColor = System.Drawing.Color.Gray;
		this.label2.Location = new System.Drawing.Point(472, 269);
		this.label2.Name = "label2";
		this.label2.Size = new System.Drawing.Size(193, 30);
		this.label2.TabIndex = 9;
		this.label2.Text = "         Select a Minecraft to use\r\nMake sure game is fully loaded first";
		this.label2.Visible = false;
		this.pictureBox1.Image = Puff.Properties.Resources._20250529_004353;
		this.pictureBox1.Location = new System.Drawing.Point(468, 222);
		this.pictureBox1.Name = "pictureBox1";
		this.pictureBox1.Size = new System.Drawing.Size(153, 33);
		this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
		this.pictureBox1.TabIndex = 2;
		this.pictureBox1.TabStop = false;
		base.AccessibleRole = System.Windows.Forms.AccessibleRole.ScrollBar;
		base.AutoScaleDimensions = new System.Drawing.SizeF(7f, 15f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		this.BackColor = System.Drawing.Color.FromArgb(24, 24, 24);
		base.ClientSize = new System.Drawing.Size(1088, 625);
		base.Controls.Add((System.Windows.Forms.Control)(object)this.guna2Shapes2);
		base.Controls.Add((System.Windows.Forms.Control)(object)this.guna2Shapes1);
		base.Controls.Add((System.Windows.Forms.Control)(object)this.guna2Button1);
		base.Controls.Add((System.Windows.Forms.Control)(object)this.guna2Panel1);
		base.Controls.Add(this.label1);
		base.Controls.Add(this.pictureBox1);
		base.Controls.Add(this.label2);
		base.Controls.Add((System.Windows.Forms.Control)(object)this.guna2ProgressBar1);
		this.Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.ForeColor = System.Drawing.Color.FromArgb(129, 130, 132);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
		base.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
		base.Name = "Form1";
		base.ShowIcon = false;
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "Vape V4 Loader";
		((System.Windows.Forms.Control)(object)this.guna2Panel1).ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.pictureBox1).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
