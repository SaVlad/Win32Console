using System;

class Program {
	public static void Main(string[] args) {
		Win32Console.SetConsoleInputMode(ConsoleInputMode.ExtendedFlags | ConsoleInputMode.MouseInput);
		ConsoleButton cb1 = new ConsoleButton {
			X = 10,
			Y = 10,
			Width = 10,
			Height = 1,
			Text = "Button 1"
		};
		ConsoleButton cb2 = new ConsoleButton {
			X = 40,
			Y = 10,
			Width = 10,
			Height = 1,
			Text = "Button 2"
		};
		void handler(ConsoleButton cb) => Win32Console.PutString(0, 0, cb.Text);
		cb1.Pressed += handler;
		cb2.Pressed += handler;
		cb1.Draw();
		cb2.Draw();
		foreach(IInputEvent ev in Win32Console.PumpInputEvents(true)) {
			if(ev is MouseInputEvent mie)
				Win32Console.PutString(string.Format("{0} {1} {2},{3}             ", mie.Type, mie.LeftButton, mie.MouseX, mie.MouseY));
			if(!cb1.OnInputEvent(ev))
				cb2.OnInputEvent(ev);
		}
	}
}
class ConsoleButton {
	public event Action<ConsoleButton> Pressed;
	public short X { get; set; }
	public short Y { get; set; }
	public short Width { get; set; }
	public short Height { get; set; }
	public string Text { get; set; }

	public void Draw() {
		if(Height <= 0)
			Height = 1;
		if(Width <= 0)
			Width = 1;
		for(short y = 0; y < Height; ++y)
			for(short x = 0; x < Width; ++x)
				Win32Console.PutChar((short) (x + X), (short) (y + Y), ' ', ConsoleColor.Black, ConsoleColor.Gray);
		short sx = 0;
		short sy = 0;
		string s = Text;
		if(Height % 2 == 0)
			sy = (short) (Height / 2);
		else
			sy = (short) ((Height - 1) / 2);
		sx = (short) ((Width - Text?.Length ?? 0) / 2);
		if(Text.Length >= Width) {
			sx = 0;
			if(Text.Length > Width)
				s = s.Substring(0, Width);
		}
		Win32Console.PutString((short)(X + sx), (short)(Y + sy), s);
	}
	public bool OnInputEvent(IInputEvent iie) {
		if(iie is MouseInputEvent mie &&
			mie.Type == MouseInputEventType.Click &
			mie.LeftButton) {
			if(mie.MouseX >= X && mie.MouseX < (X + Width) &&
				mie.MouseY >= Y && mie.MouseY < (Y + Height)) {
				Pressed?.Invoke(this);
				return true;
			}
		}
		return false;
	}
}