using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

// A non-activating real Windows window for native full-screen regression checks.
class Fixture : Form
{
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int height, uint flags);
    string file;
    protected override bool ShowWithoutActivation { get { return true; } }
    Fixture(string file)
    {
        this.file = file; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; BackColor = Color.Black;
        Bounds = Screen.PrimaryScreen.Bounds;
        Shown += delegate { var b = Bounds; SetWindowPos(Handle, new IntPtr(1), b.X, b.Y, b.Width, b.Height, 0x0010); File.WriteAllText(file + ".handle", Handle.ToInt64().ToString()); };
        var timer = new Timer { Interval = 100 };
        timer.Tick += delegate
        {
            if (!File.Exists(file)) return;
            string command; try { command = File.ReadAllText(file); } catch { return; }
            if (command == "quit") Close();
            else if (command == "normal") { WindowState = FormWindowState.Normal; Bounds = new Rectangle(20, 20, 600, 400); }
            else if (command == "minimize") WindowState = FormWindowState.Minimized;
            else if (command == "full") { WindowState = FormWindowState.Normal; Bounds = Screen.PrimaryScreen.Bounds; }
        }; timer.Start();
    }
    [STAThread] static void Main(string[] args) { Application.Run(new Fixture(args[0])); }
}
