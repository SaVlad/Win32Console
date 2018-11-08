using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using static Win32Console.NativeApi;

public static class Win32Console {
	private static CONSOLE_SCREEN_BUFFER_INFO ScreenBufferInfo {
		get {
			if(!GetConsoleScreenBufferInfo(OutputHandle, out CONSOLE_SCREEN_BUFFER_INFO csbi))
				ThrowWin32("Failed to get console buffer info");
			return csbi;
		}
	}
	private static Stack<ConsoleOutputMode> StoredModesOutput { get; } = new Stack<ConsoleOutputMode>();
	private static Stack<ConsoleInputMode> StoredModesInput { get; } = new Stack<ConsoleInputMode>();
	public static IntPtr InputHandle { get; private set; } = NULL;
	public static IntPtr OutputHandle { get; private set; } = NULL;
	public static bool OutputAvailable => OutputHandle != NULL;
	public static bool InputAvailable => InputHandle != NULL;
	public static short CursorX {
		get {
			if(!OutputAvailable)
				return -1;
			return ScreenBufferInfo.dwCursorPosition.X;
		}
		set {
			if(OutputAvailable && !SetConsoleCursorPosition(OutputHandle, new COORD { X = value, Y = CursorY }))
				ThrowWin32("Failed to set cursor position");
		}
	}
	public static short CursorY {
		get {
			if(!OutputAvailable)
				return -1;
			return ScreenBufferInfo.dwCursorPosition.Y;
		}
		set {
			if(OutputAvailable && !SetConsoleCursorPosition(OutputHandle, new COORD { X = CursorX, Y = value }))
				ThrowWin32("Failed to set cursor position");
		}
	}
	public static short BufferWidth {
		get {
			if(!OutputAvailable)
				return -1;
			return ScreenBufferInfo.dwSize.X;
		}
		set {
			if(OutputAvailable && !SetConsoleScreenBufferSize(OutputHandle, new COORD { X = value, Y = BufferHeight }))
				ThrowWin32("Failed to set console buffer size");
		}
	}
	public static short BufferHeight {
		get {
			if(!OutputAvailable)
				return -1;
			return ScreenBufferInfo.dwSize.Y;
		}
		set {
			if(OutputAvailable && !SetConsoleScreenBufferSize(OutputHandle, new COORD { X = BufferWidth, Y = value }))
				ThrowWin32("Failed to set console buffer size");
		}
	}
	public static short WindowWidth {
		get {
			if(!OutputAvailable)
				return -1;
			return (short) (ScreenBufferInfo.srWindow.Right - ScreenBufferInfo.srWindow.Left);
		}
		set {
			SMALL_RECT sr = new SMALL_RECT {
				Left = 0,
				Top = 0,
				Bottom = WindowHeight,
				Right = value
			};
			if(OutputAvailable && !SetConsoleWindowInfo(OutputHandle, false, ref sr))
				ThrowWin32("Faile to set console window info");
		}
	}
	public static short WindowHeight {
		get {
			if(!OutputAvailable)
				return -1;
			return (short) (ScreenBufferInfo.srWindow.Bottom - ScreenBufferInfo.srWindow.Top);
		}
		set {
			SMALL_RECT sr = new SMALL_RECT {
				Left = 0,
				Top = 0,
				Bottom = value,
				Right = WindowWidth
			};
			if(OutputAvailable && !SetConsoleWindowInfo(OutputHandle, false, ref sr))
				ThrowWin32("Faile to set console window info");
		}
	}
	public static ConsoleColor ForegroundColor {
		get {
			if(!OutputAvailable)
				return ConsoleColor.Gray;
			return (ConsoleColor) (ScreenBufferInfo.wAttributes & 0x0F);
		}
		set {
			if(OutputAvailable && !SetConsoleTextAttribute(OutputHandle, (ushort) ((short) value | (short) ((short) BackgroundColor << 4))))
				ThrowWin32("Failed to set console text attribute");
		}
	}
	public static ConsoleColor BackgroundColor {
		get {
			if(!OutputAvailable)
				return ConsoleColor.Black;
			return (ConsoleColor) ((ScreenBufferInfo.wAttributes & 0xF0) >> 4);
		}
		set {
			if(OutputAvailable && !SetConsoleTextAttribute(OutputHandle, (ushort) ((short) ForegroundColor | (short) ((short) value << 4))))
				ThrowWin32("Failed to set console text attribute");
		}
	}
	public static string Title {
		get {
			if(!OutputAvailable)
				return null;
			StringBuilder sb = new StringBuilder();
			if(GetConsoleTitle(sb, 64 * 1024 - 1) == 0)
				ThrowWin32("Failed to retrieve console title");
			return sb.ToString();
		}
		set {
			if(OutputAvailable && !SetConsoleTitle(value))
				ThrowWin32("Failed to set console title");
		}
	}
	public static bool IsCursorVisible {
		get {
			if(!OutputAvailable)
				return false;
			if(!GetConsoleCursorInfo(OutputHandle, out CONSOLE_CURSOR_INFO cci))
				ThrowWin32("Failed to retrieve cursor info");
			return cci.bVisible;
		}
		set {
			if(!OutputAvailable)
				return;
			if(!GetConsoleCursorInfo(OutputHandle, out CONSOLE_CURSOR_INFO cci))
				ThrowWin32("Failed to retrieve cursor info");
			cci.bVisible = value;
			if(!SetConsoleCursorInfo(OutputHandle, ref cci))
				ThrowWin32("Failed to set cursor info");
		}
	}
	public static int InputEventsCount {
		get {
			if(!OutputAvailable)
				return 0;
			if(!GetNumberOfConsoleInputEvents(InputHandle, out uint c))
				ThrowWin32("Failed to get number of console input events");
			return (int) c;
		}
	}
	public static ConsoleOutputMode ConsoleOutputMode {
		get {
			if(!OutputAvailable)
				return 0;
			if(!GetConsoleMode(OutputHandle, out uint mode))
				ThrowWin32("Failed to get console output mode");
			return (ConsoleOutputMode) mode;
		}
	}
	public static ConsoleInputMode ConsoleInputMode {
		get {
			if(!OutputAvailable)
				return 0;
			if(!GetConsoleMode(InputHandle, out uint mode))
				ThrowWin32("Failed to get console input mode");
			return (ConsoleInputMode) mode;
		}
	}
	static Win32Console() {
		InputHandle = GetStdHandle(STD_INPUT_HANDLE);
		if(InputHandle == INVALID_HANDLE_VALUE)
			throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to retrieve STD_INPUT_HANDLE");
		OutputHandle = GetStdHandle(STD_OUTPUT_HANDLE);
		if(OutputHandle == INVALID_HANDLE_VALUE)
			throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to retrieve STD_OUTPUT_HANDLE");
	}

	private static void ThrowWin32(string message) {
		try {
			throw new Win32Exception(Marshal.GetLastWin32Error());
		} catch(Win32Exception exc) {
			throw new Win32Exception($"{message}. {exc.Message}");
		}
	}
	private static int SizeOf<T>() where T : struct => SizeOf(new T());
	private static int SizeOf<T>(T obj) where T : struct => Marshal.SizeOf(obj);
	private static ulong BuildCtrlWakeupMask(char[] chars) {
		ulong mask = 0;
		foreach(char c in chars)
			mask |= 1UL << c;
		return mask;
	}
	private static IntPtr MallocStructUnmanaged<T>(T obj) where T : struct {
		IntPtr ptr = Marshal.AllocHGlobal(SizeOf(obj));
		Marshal.StructureToPtr(obj, ptr, false);
		return ptr;
	}
	private static void FreeStructUnmanaged<T>(IntPtr ptr) => Marshal.DestroyStructure(ptr, typeof(T));
	private static INPUT_RECORD NextInputRecord() {
		INPUT_RECORD[] ir = new[] { new INPUT_RECORD() };
		if(!ReadConsoleInput(InputHandle, ir, 1, out uint l))
			ThrowWin32("Failed to read console input");
		return ir[0];
	}
	private static void SeparateModes(uint mode, out ConsoleOutputMode o, out ConsoleInputMode i) {
		const uint output_mask =
			ENABLE_PROCESSED_OUTPUT | ENABLE_WRAP_AT_EOL_OUTPUT |
			ENABLE_VIRTUAL_TERMINAL_PROCESSING | ENABLE_NEWLINE_AUTO_RETURN |
			ENABLE_LVB_GRID_WORLDWIDE;
		const uint input_mask =
			ENABLE_ECHO_INPUT | ENABLE_INSERT_MODE | ENABLE_LINE_INPUT |
			ENABLE_MOUSE_INPUT | ENABLE_PROCESSED_INPUT | ENABLE_QUICK_EDIT_MODE |
			ENABLE_WINDOW_INPUT | ENABLE_VIRTUAL_TERMINAL_INPUT;
		o = (ConsoleOutputMode) (mode & output_mask);
		i = (ConsoleInputMode) (mode & input_mask);
	}
	private static bool Wait(int timeout, ThreadStart ts, bool abort) {
		ManualResetEvent _lock = new ManualResetEvent(false);
		bool timedout = false;
		Thread th = new Thread(() => {
			try {
				ts();
				_lock.Set();
			} catch(ThreadAbortException) { }
		}) { IsBackground = true };
		Thread wait = new Thread(() => {
			try {
				Thread.Sleep(timeout);
				timedout = true;
				_lock.Set();
			} catch(ThreadAbortException) { }
		}) { IsBackground = true };
		th.Start();
		if(timeout > 0)
			wait.Start();
		_lock.WaitOne();
		if(!timedout)
			wait.Abort();
		if(timedout && abort)
			th.Abort();
		return timedout;
	}

	public static void InitConsole() {
		if(OutputHandle == NULL && !AllocConsole())
			ThrowWin32("Failed to allocate console");
		InputHandle = GetStdHandle(STD_INPUT_HANDLE);
		if(InputHandle == INVALID_HANDLE_VALUE)
			ThrowWin32("Failed to retrieve STD_INPUT_HANDLE");
		OutputHandle = GetStdHandle(STD_OUTPUT_HANDLE);
		if(OutputHandle == INVALID_HANDLE_VALUE)
			ThrowWin32("Failed to retrieve STD_OUTPUT_HANDLE");
	}
	public static int Write(object o = null) {
		if(!OutputAvailable || o == null)
			return 0;
		string s = o.ToString();
		if(s.Length == 0)
			return 0;
		if(!WriteConsole(OutputHandle, s, (uint) s.Length, out uint l, NULL))
			ThrowWin32("Failed to write to console");
		return (int) l;
	}
	public static int WriteFormat(string s = null, params object[] o) => Write(string.Format(s ?? "", o));
	public static int WriteLine(object o = null) => Write((o?.ToString() ?? "") + "\r\n");
	public static int WriteLineFormat(string s = null, params object[] o) => WriteLine(string.Format(s ?? "", o));
	public static void PutChar(char c) => PutChar(CursorX, CursorY, c);
	public static void PutChar(short x, short y, char c) {
		if(!OutputAvailable)
			return;
		if(!WriteConsoleOutputCharacter(OutputHandle, c.ToString(), 1, new COORD { X = x, Y = y }, out uint l))
			ThrowWin32("Failed to write console output character");
	}
	public static void PutString(string s) => PutString(CursorX, CursorY, s ?? "");
	public static void PutString(short x, short y, string s) {
		if(s == null || s.Length == 0)
			return;
		COORD coord = new COORD { X = x, Y = y };
		foreach(char c in s) {
			switch(c) {
				case '\r':
					coord.X = 0;
					break;
				case '\n':
					coord.Y++;
					break;
				case '\b':
					if(coord.X > 0)
						coord.X--;
					break;
				case '\f':
					coord = new COORD();
					break;
				case '\0':
					return;
				default:
					PutChar(coord.X, coord.Y, c);
					if(++coord.X >= BufferWidth) {
						coord.X = 0;
						coord.Y++;
					}
					break;
			}
		}
	}
	public static void SetColor(ConsoleColor fore, ConsoleColor back) => SetColor(CursorX, CursorY, fore, back);
	public static void SetColor(short x, short y, ConsoleColor fore, ConsoleColor back) {
		if(!OutputAvailable)
			return;
		if(!WriteConsoleOutputAttribute(OutputHandle, new ushort[] { (ushort) ((short) fore | (short) ((short) back << 4)) }, 1, new COORD { X = x, Y = y }, out uint l))
			ThrowWin32("Failed to write console output attribute");
	}
	public static char GetChar() => GetChar(CursorX, CursorY);
	public static char GetChar(short x, short y) {
		if(!OutputAvailable)
			return '\0';
		StringBuilder sb = new StringBuilder();
		if(!ReadConsoleOutputCharacter(OutputHandle, sb, 1, new COORD { X = x, Y = y }, out uint l))
			ThrowWin32("Failed to read console output character");
		return sb.ToString()[0];
	}
	public static string GetString(int length) => GetString(CursorX, CursorY, length);
	public static string GetString(short x, short y, int length) {
		if(!OutputAvailable)
			return null;
		StringBuilder sb = new StringBuilder();
		if(!ReadConsoleOutputCharacter(OutputHandle, sb, (uint) length, new COORD { X = x, Y = y }, out uint l))
			ThrowWin32("Failed to read console output character");
		return sb.ToString();
	}
	public static ConsoleColor GetForegroundColor() => GetForegroundColor(CursorX, CursorY);
	public static ConsoleColor GetForegroundColor(short x, short y) {
		if(!OutputAvailable)
			return ForegroundColor;
		ushort[] attr = new ushort[1];
		if(!ReadConsoleOutputAttribute(OutputHandle, attr, 1, new COORD { X = x, Y = y }, out uint l))
			ThrowWin32("Failed to read console output attribute");
		return (ConsoleColor) (attr[0] & 0xF);
	}
	public static ConsoleColor GetBackgroundColor() => GetBackgroundColor(CursorX, CursorY);
	public static ConsoleColor GetBackgroundColor(short x, short y) {
		if(!OutputAvailable)
			return ForegroundColor;
		ushort[] attr = new ushort[1];
		if(!ReadConsoleOutputAttribute(OutputHandle, attr, 1, new COORD { X = x, Y = y }, out uint l))
			ThrowWin32("Failed to read console output attribute");
		return (ConsoleColor) ((attr[0] & 0xF0) >> 4);
	}

	public static string Read(int count) {
		if(!InputAvailable)
			return null;
		StringBuilder sb = new StringBuilder();
		CONSOLE_READCONSOLE_CONTROL crcc = new CONSOLE_READCONSOLE_CONTROL {
			nLength = (ulong) SizeOf<CONSOLE_READCONSOLE_CONTROL>(),
			nInitialChars = 0,
			dwCtrlWakeupMask = 0,
			dwControlKeyState = 0
		};
		IntPtr ptr = MallocStructUnmanaged(crcc);
		if(!ReadConsole(InputHandle, sb, (uint) count, out uint l, ptr)) {
			FreeStructUnmanaged<CONSOLE_READCONSOLE_CONTROL>(ptr);
			ThrowWin32("Failed to read console");
		}
		FreeStructUnmanaged<CONSOLE_READCONSOLE_CONTROL>(ptr);
		return sb.ToString();
	}
	public static string Read(params char[] stopChars) {
		if(!InputAvailable)
			return null;
		StringBuilder sb = new StringBuilder();
		CONSOLE_READCONSOLE_CONTROL crcc = new CONSOLE_READCONSOLE_CONTROL {
			nLength = (ulong) SizeOf<CONSOLE_READCONSOLE_CONTROL>(),
			nInitialChars = 0,
			dwCtrlWakeupMask = BuildCtrlWakeupMask(stopChars),
			dwControlKeyState = 0
		};
		IntPtr ptr = MallocStructUnmanaged(crcc);
		if(!ReadConsole(InputHandle, sb, 64U * 1024U, out uint l, ptr)) {
			FreeStructUnmanaged<CONSOLE_READCONSOLE_CONTROL>(ptr);
			ThrowWin32("Failed to read console");
		}
		FreeStructUnmanaged<CONSOLE_READCONSOLE_CONTROL>(ptr);
		return sb.ToString();
	}
	public static IInputEvent PeekInputEvent() {
		if(!InputAvailable)
			return null;
		INPUT_RECORD[] ir = new[] { new INPUT_RECORD() };
		while(true) {
			if(!PeekConsoleInput(InputHandle, ir, 1, out uint l))
				ThrowWin32("Failed to peek console input");
			if(l == 0)
				return null;
			switch(ir[0].EventType) {
				case KEY_EVENT: return new KeyInputEvent(ir[0].KeyEvent);
				case MOUSE_EVENT: return new MouseInputEvent(ir[0].MouseEvent);
				case WINDOW_BUFFER_SIZE_EVENT: return new WindowBufferSizeInputEvent(ir[0].WindowBufferSizeEvent);
				default: NextInputRecord(); break;
			}
		}
	}
	public static IInputEvent NextInputEvent() {
		if(!InputAvailable)
			return null;
		INPUT_RECORD[] ir = new[] { new INPUT_RECORD() };
		while(true) {
			if(!ReadConsoleInput(InputHandle, ir, 1, out uint l))
				ThrowWin32("Failed to peek console input");
			if(l == 0)
				return null;
			switch(ir[0].EventType) {
				case KEY_EVENT: return new KeyInputEvent(ir[0].KeyEvent);
				case MOUSE_EVENT: return new MouseInputEvent(ir[0].MouseEvent);
				case WINDOW_BUFFER_SIZE_EVENT: return new WindowBufferSizeInputEvent(ir[0].WindowBufferSizeEvent);
				default: NextInputRecord(); break;
			}
		}
	}
	public static void WriteInput(string str) {
		if(!InputAvailable || str == null || str.Length == 0)
			return;
		string s = new string(str.Where(chr => !char.IsControl(chr)).ToArray());
		INPUT_RECORD[] ir = new INPUT_RECORD[s.Length * 2];
		for(int i = 0, si = 0; i < ir.Length; i = (++si) * 2) {
			ir[i] = new INPUT_RECORD {
				EventType = KEY_EVENT,
				KeyEvent = new KEY_EVENT_RECORD {
					bKeyDown = true,
					UnicodeChar = s[si],
					wVirtualKeyCode = s[si],
					wRepeatCount = 1
				}
			};
			ir[i + 1] = new INPUT_RECORD {
				EventType = KEY_EVENT,
				KeyEvent = new KEY_EVENT_RECORD {
					bKeyDown = false,
					UnicodeChar = s[si],
					wRepeatCount = 1
				}
			};
		}
		if(!WriteConsoleInput(InputHandle, ir, (uint) ir.Length, out uint l))
			ThrowWin32("Failed to write console input");
	}
	public static bool WaitForInputEvent(int timeout) {
		if(!InputAvailable)
			return false;
		return !Wait(timeout, () => {
			while(InputEventsCount == 0)
				Thread.Sleep(100);
		}, true);
	}

	public static void SetConsoleOutputMode(ConsoleOutputMode mode) {
		if(!OutputAvailable)
			return;
		if(!SetConsoleMode(OutputHandle, (uint) mode))
			ThrowWin32("Failed to set console output mode");
	}
	public static void SetConsoleInputMode(ConsoleInputMode mode) {
		if(!InputAvailable)
			return;
		if(!SetConsoleMode(InputHandle, (uint) mode))
			ThrowWin32("Failed to set console input mode");
	}
	public static ConsoleOutputMode PushConsoleOutputMode(ConsoleOutputMode mode) {
		if(!OutputAvailable)
			return 0;
		ConsoleOutputMode old = ConsoleOutputMode;
		StoredModesOutput.Push(old);
		SetConsoleOutputMode(mode);
		return old;
	}
	public static ConsoleInputMode PushConsoleInputMode(ConsoleInputMode mode) {
		if(!InputAvailable)
			return 0;
		ConsoleInputMode old = ConsoleInputMode;
		StoredModesInput.Push(old);
		SetConsoleInputMode(mode);
		return old;
	}
	public static ConsoleOutputMode PopConsoleOutputMode() {
		if(!OutputAvailable || StoredModesOutput.Count == 0)
			return 0;
		ConsoleOutputMode old = ConsoleOutputMode;
		SetConsoleOutputMode(StoredModesOutput.Pop());
		return old;
	}
	public static ConsoleInputMode PopConsoleInputMode() {
		if(!InputAvailable || StoredModesInput.Count == 0)
			return 0;
		ConsoleInputMode old = ConsoleInputMode;
		SetConsoleInputMode(StoredModesInput.Pop());
		return old;
	}
	public static void SetConsoleOutputModeFlags(ConsoleOutputMode flags) {
		if(!OutputAvailable)
			return;
		SetConsoleOutputMode(ConsoleOutputMode | flags);
	}
	public static void SetConsoleInputModeFlags(ConsoleInputMode flags) {
		if(!InputAvailable)
			return;
		SetConsoleInputMode(ConsoleInputMode | flags);
	}
	public static void ClearConsoleOutputModeFlags(ConsoleOutputMode flags) {
		if(!OutputAvailable)
			return;
		SetConsoleOutputMode(ConsoleOutputMode & ~flags);
	}
	public static void ClearConsoleInputModeFlags(ConsoleInputMode flags) {
		if(!InputAvailable)
			return;
		SetConsoleInputMode(ConsoleInputMode & ~flags);
	}
	public static void ToggleConsoleOutputModeFlags(ConsoleOutputMode flags) {
		if(!OutputAvailable)
			return;
		SetConsoleOutputMode(ConsoleOutputMode ^ flags);
	}
	public static void ToggleConsoleInputModeFlags(ConsoleInputMode flags) {
		if(!InputAvailable)
			return;
		SetConsoleInputMode(ConsoleInputMode ^ flags);
	}

	public static class NativeApi {
		#region Constants
		public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
		public static readonly IntPtr NULL = IntPtr.Zero;
		public const uint STD_INPUT_HANDLE = unchecked((uint) -10);
		public const uint STD_OUTPUT_HANDLE = unchecked((uint) -11);
		public const uint STD_ERROR_HANDLE = unchecked((uint) -12);
		public const uint CONSOLE_NO_SELECTION = 0x0000;
		public const uint CONSOLE_SELECTION_IN_PROGRESS = 0x0001;
		public const uint CONSOLE_SELECTION_NOT_EMPTY = 0x0002;
		public const uint CONSOLE_MOUSE_SELECTION = 0x0004;
		public const uint CONSOLE_MOUSE_DOWN = 0x0008;
		public const uint ERROR_NOT_ENOUGH_MEMORY = 0x8;
		public const ushort FOCUS_EVENT = 0x0010;
		public const ushort KEY_EVENT = 0x0001;
		public const ushort MENU_EVENT = 0x0008;
		public const ushort MOUSE_EVENT = 0x0002;
		public const ushort WINDOW_BUFFER_SIZE_EVENT = 0x0004;
		public const uint CAPSLOCK_ON = 0x0080;
		public const uint ENHANCED_KEY = 0x0100;
		public const uint LEFT_ALT_PRESSED = 0x0002;
		public const uint LEFT_CTRL_PRESSED = 0x0008;
		public const uint NUMLOCK_ON = 0x0020;
		public const uint RIGHT_ALT_PRESSED = 0x0001;
		public const uint RIGHT_CTRL_PRESSED = 0x0004;
		public const uint SCROLLLOCK_ON = 0x0040;
		public const uint SHIFT_PRESSED = 0x0010;
		public const uint FROM_LEFT_1ST_BUTTON_PRESSED = 0x0001;
		public const uint FROM_LEFT_2ND_BUTTON_PRESSED = 0x0004;
		public const uint FROM_LEFT_3RD_BUTTON_PRESSED = 0x0008;
		public const uint FROM_LEFT_4TH_BUTTON_PRESSED = 0x0010;
		public const uint RIGHTMOST_BUTTON_PRESSED = 0x0002;
		public const uint DOUBLE_CLICK = 0x0002;
		public const uint MOUSE_HWHEELED = 0x0008;
		public const uint MOUSE_MOVED = 0x0001;
		public const uint MOUSE_WHEELED = 0x0004;
		public const ushort FOREGROUND_BLUE = 0x0001;
		public const ushort FOREGROUND_GREEN = 0x0002;
		public const ushort FOREGROUND_RED = 0x0003;
		public const ushort FOREGROUND_INTENSITY = 0x0008;
		public const ushort BACKGROUND_BLUE = 0x0010;
		public const ushort BACKGROUND_GREEN = 0x0020;
		public const ushort BACKGROUND_RED = 0x0040;
		public const ushort BACKGROUND_INTENSITY = 0x0080;
		public const ushort COMMON_LVB_LEADING_BYTE = 0x0100;
		public const ushort COMMON_LVB_TRAILING_BYTE = 0x0200;
		public const ushort COMMON_LVB_GRID_HORIZONTAL = 0x0400;
		public const ushort COMMON_LVB_GRID_LVERTICAL = 0x0800;
		public const ushort COMMON_LVB_GRID_RVERTICAL = 0x1000;
		public const ushort COMMON_LVB_REVERSE_VIDEO = 0x4000;
		public const ushort COMMON_LVB_UNDERSCORE = 0x8000;
		public const uint SM_CXMIN = 28;
		public const uint SM_CYMIN = 29;
		public const uint ENABLE_ECHO_INPUT = 0x0004;
		public const uint ENABLE_EXTENDED_FLAGS = 0x0080;
		public const uint ENABLE_INSERT_MODE = 0x0020;
		public const uint ENABLE_LINE_INPUT = 0x0002;
		public const uint ENABLE_MOUSE_INPUT = 0x0010;
		public const uint ENABLE_PROCESSED_INPUT = 0x0001;
		public const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
		public const uint ENABLE_WINDOW_INPUT = 0x0008;
		public const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200;
		public const uint ENABLE_PROCESSED_OUTPUT = 0x0001;
		public const uint ENABLE_WRAP_AT_EOL_OUTPUT = 0x0002;
		public const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
		public const uint ENABLE_NEWLINE_AUTO_RETURN = 0x0008;
		public const uint ENABLE_LVB_GRID_WORLDWIDE = 0x0010;
		#endregion

		#region Functions
		/// <summary>
		/// Retrieves information about the specified console screen buffer.
		/// </summary>
		/// <param name="hConsoleOutput">
		/// A handle to the console screen buffer. The handle must have the GENERIC_READ
		/// access right.
		/// </param>
		/// <param name="lpConsoleScreenBufferInfo">
		/// A pointer to a <see cref="CONSOLE_SCREEN_BUFFER_INFO"/> structure that receives
		/// the console screen buffer information.
		/// </param>
		/// <returns>Function success</returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool GetConsoleScreenBufferInfo(
			[In] IntPtr hConsoleOutput,
			[Out] out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);
		/// <summary>
		/// Retrieves information about the size and visibility of the cursor for the
		/// specified console screen buffer.
		/// </summary>
		/// <param name="hConsoleOutput">
		/// A handle to the console screen buffer. The handle must have the GENERIC_READ 
		/// access right.
		/// </param>
		/// <param name="lpConsoleCursorInfo">
		/// A pointer to a <see cref="CONSOLE_CURSOR_INFO"/> structure that receives
		/// information about the console's cursor.
		/// </param>
		/// <returns></returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool GetConsoleCursorInfo(
			[In] IntPtr hConsoleOutput,
			[Out] out CONSOLE_CURSOR_INFO lpConsoleCursorInfo);
		/// <summary>
		/// Sets the size and visibility of the cursor for the specified console screen buffer.
		/// </summary>
		/// <param name="hConsoleOutput">
		/// A handle to the console screen buffer. The handle must have the GENERIC_READ
		/// access right.
		/// </param>
		/// <param name="lpConsoleCursorInfo">
		/// A pointer to a <see cref="CONSOLE_CURSOR_INFO"/> structure that provides the
		/// new specifications for the console screen buffer's cursor.
		/// </param>
		/// <returns>Function success.</returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool SetConsoleCursorInfo(
			[In] IntPtr hConsoleOutput,
			[In] ref CONSOLE_CURSOR_INFO lpConsoleCursorInfo);
		/// <summary>
		/// Allocates a new console for the calling process.
		/// </summary>
		/// <returns>Function success</returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool AllocConsole();
		/// <summary>
		/// Retrieves a handle to the specified standard device
		/// (standard input, standard output, or standard error).
		/// </summary>
		/// <param name="nStdHandle">
		/// The standard device. This parameter can be one of the STD_* values.
		/// </param>
		/// <returns>
		/// If the function succeeds, the return value is a handle to the specified device,
		/// or a redirected handle set by a previous call to SetStdHandle. The handle has
		/// GENERIC_READ and GENERIC_WRITE access rights, unless the application has used
		/// SetStdHandle to set a standard handle with lesser access.
		/// If the function fails, the return value is <see cref="INVALID_HANDLE_VALUE"/>.
		/// </returns>
		[DllImport("kernel32", SetLastError = true)]
		public static extern IntPtr GetStdHandle(
			[In] uint nStdHandle);
		/// <summary>
		/// Writes a character string to a console screen buffer beginning at the current
		/// cursor location.
		/// </summary>
		/// <param name="hConsoleOutput">
		/// A handle to the console screen buffer. The handle must have the GENERIC_WRITE
		/// access right.
		/// </param>
		/// <param name="lpBuffer">
		/// A pointer to a buffer that contains characters to be written to the console
		/// screen buffer.
		/// </param>
		/// <param name="nNumberOfCharsToWrite">
		/// The number of characters to be written. If the total size of the specified number
		/// of characters exceeds the available heap, the function fails with
		/// <see cref="ERROR_NOT_ENOUGH_MEMORY"/>.
		/// </param>
		/// <param name="lpNumberOfCharsWritten">
		/// A pointer to a variable that receives the number of characters actually written.
		/// </param>
		/// <param name="lpReserved">Reserved; must be <see cref="NULL"/>.</param>
		/// <returns>Function success.</returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool WriteConsole(
			[In] IntPtr hConsoleOutput,
			[In] string lpBuffer,
			[In] uint nNumberOfCharsToWrite,
			[Out] out uint lpNumberOfCharsWritten,
			IntPtr lpReserved);
		/// <summary>
		/// Retrieves the title for the current console window.
		/// </summary>
		/// <param name="lpConsoleTitle">
		/// A pointer to a buffer that receives a null-terminated string containing the title.
		/// If the buffer is too small to store the title, the function stores as many
		/// characters of the title as will fit in the buffer, ending with a null terminator.
		/// </param>
		/// <param name="nSize">
		/// The size of the buffer pointed to by the <paramref name="lpConsoleTitle"/>
		/// parameter, in characters.
		/// </param>
		/// <returns>
		/// If the function succeeds, the return value is the length of the console window's
		/// title, in characters.
		/// </returns>
		[DllImport("kernel32", SetLastError = true)]
		public static extern uint GetConsoleTitle(
			[Out] StringBuilder lpConsoleTitle,
			[In] uint nSize);
		/// <summary>
		/// Retrieves the window handle used by the console associated with the calling
		/// process.
		/// </summary>
		/// <returns>
		/// The return value is a handle to the window used by the console associated with
		/// the calling process or <see cref="NULL"/> if there is no such associated console.
		/// </returns>
		[DllImport("kernel32", SetLastError = true)]
		public static extern IntPtr GetConsoleWindow();
		/// <summary>
		/// Reads data from the specified console input buffer without removing it from the
		/// buffer.
		/// </summary>
		/// <param name="hConsoleInput">
		/// A handle to the console input buffer. The handle must have the GENERIC_READ
		/// access right.
		/// </param>
		/// <param name="lpBuffer">
		/// A pointer to an array of <see cref="INPUT_RECORD"/> structures that receives the
		/// input buffer data.
		/// </param>
		/// <param name="nLength">
		/// The size of the array pointed to by the <paramref name="lpBuffer"/> parameter,
		/// in array elements.
		/// </param>
		/// <param name="lpNumberOfEventsRead">
		/// A pointer to a variable that receives the number of input records read.
		/// </param>
		/// <returns>Function success.</returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool PeekConsoleInput(
			[In] IntPtr hConsoleInput,
			[MarshalAs(UnmanagedType.LPArray), Out] INPUT_RECORD[] lpBuffer,
			[In] uint nLength,
			[Out] out uint lpNumberOfEventsRead);
		/// <summary>
		/// Reads character input from the console input buffer and removes it from the buffer.
		/// </summary>
		/// <param name="hConsoleInput">
		/// A handle to the console input buffer. The handle must have the GENERIC_READ
		/// access right.
		/// </param>
		/// <param name="lpBuffer">
		/// A pointer to a buffer that receives the data read from the console input buffer.
		/// </param>
		/// <param name="nNumberOfCharsToRead">
		/// The number of characters to be read.
		/// </param>
		/// <param name="lpNumberOfCharsRead">
		/// A pointer to a variable that receives the number of characters actually read.
		/// </param>
		/// <param name="pInputControl">
		/// A pointer to a <see cref="CONSOLE_READCONSOLE_CONTROL"/> structure that specifies
		/// a control character to signal the end of the read operation. This parameter can
		/// be <see cref="NULL"/>. This parameter requires Unicode input by default. For ANSI
		/// mode, set this parameter to <see cref="NULL"/>.
		/// </param>
		/// <returns>Function success.</returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool ReadConsole(
			[In] IntPtr hConsoleInput,
			[Out] StringBuilder lpBuffer,
			[In] uint nNumberOfCharsToRead,
			[Out] out uint lpNumberOfCharsRead,
			[In, Optional] IntPtr pInputControl);
		/// <summary>
		/// Reads data from a console input buffer and removes it from the buffer.
		/// </summary>
		/// <param name="hConsoleInput">
		/// A handle to the console input buffer. The handle must have the GENERIC_READ
		/// access right.
		/// </param>
		/// <param name="lpBuffer">
		/// A pointer to an array of <see cref="INPUT_RECORD"/> structures that receives the
		/// input buffer data.
		/// </param>
		/// <param name="nLength">
		/// The size of the array pointed to by the <paramref name="lpBuffer"/> parameter,
		/// in array elements.
		/// </param>
		/// <param name="lpNumberOfEventsRead">
		/// A pointer to a variable that receives the number of input records read.
		/// </param>
		/// <returns>Function success.</returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool ReadConsoleInput(
			[In] IntPtr hConsoleInput,
			[MarshalAs(UnmanagedType.LPArray), Out] INPUT_RECORD[] lpBuffer,
			[In] uint nLength,
			[Out] out uint lpNumberOfEventsRead);
		/// <summary>
		/// Reads character and color attribute data from a rectangular block of character
		/// cells in a console screen buffer, and the function writes the data to a
		/// rectangular block at a specified location in the destination buffer.
		/// </summary>
		/// <param name="hConsoleOutput">
		/// A handle to the console screen buffer. The handle must have the GENERIC_READ
		/// access right.
		/// </param>
		/// <param name="lpBuffer">
		/// A pointer to a destination buffer that receives the data read from the console
		/// screen buffer. This pointer is treated as the origin of a two-dimensional array
		/// of <see cref="CHAR_INFO"/> structures whose size is specified by the
		/// <paramref name="dwBufferSize"/> parameter.
		/// </param>
		/// <param name="dwBufferSize">
		/// The size of the lpBuffer parameter, in character cells. The X member of the
		/// <see cref="COORD"/> structure is the number of columns; the Y member is the
		/// number of rows.
		/// </param>
		/// <param name="dwBufferCoord">
		/// The coordinates of the upper-left cell in the <paramref name="lpBuffer"/>
		/// parameter that receives the data read from the console screen buffer.
		/// The X member of the <see cref="COORD"/> structure is the column, and the Y member
		/// is the row.
		/// </param>
		/// <param name="lpReadRegion">
		/// A pointer to a <see cref="SMALL_RECT"/> structure. On input, the structure
		/// members specify the upper-left and lower-right coordinates of the console screen
		/// buffer rectangle from which the function is to read. On output, the structure
		/// members specify the actual rectangle that was used.
		/// </param>
		/// <returns></returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool ReadConsoleOutput(
			[In] IntPtr hConsoleOutput,
			[MarshalAs(UnmanagedType.LPArray), Out] CHAR_INFO[] lpBuffer,
			[In] COORD dwBufferSize,
			[In] COORD dwBufferCoord,
			[In, Out] ref SMALL_RECT lpReadRegion);
		/// <summary>
		/// Copies a specified number of character attributes from consecutive cells of a
		/// console screen buffer, beginning at a specified location.
		/// </summary>
		/// <param name="hConsoleOutput">
		/// A handle to the console screen buffer. The handle must have the GENERIC_READ
		/// access right.
		/// </param>
		/// <param name="lpAttribute">
		/// A pointer to a buffer that receives the attributes being used by the console
		/// screen buffer.
		/// </param>
		/// <param name="dwBufferSize">
		/// The number of screen buffer character cells from which to read.
		/// </param>
		/// <param name="dwBufferCoord">
		/// The coordinates of the first cell in the console screen buffer from which to
		/// read, in characters. The X member of the <see cref="COORD"/> structure is the
		/// column, and the Y member is the row.
		/// </param>
		/// <param name="lpReadRegion">
		/// A pointer to a variable that receives the number of attributes actually read.
		/// </param>
		/// <returns>Function success.</returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool ReadConsoleOutputAttribute(
			[In] IntPtr hConsoleOutput,
			[MarshalAs(UnmanagedType.LPArray), Out] ushort[] lpAttribute,
			[In] uint nLength,
			[In] COORD dwReadCoord,
			[Out] out uint lpNumberOfAttrsRead);
		/// <summary>
		/// Copies a number of characters from consecutive cells of a console screen buffer,
		/// beginning at a specified location.
		/// </summary>
		/// <param name="hConsoleOutput">
		/// A handle to the console screen buffer. The handle must have the GENERIC_READ
		/// access right.
		/// </param>
		/// <param name="lpCharacter">
		/// A pointer to a buffer that receives the characters read from the console screen
		/// buffer.
		/// </param>
		/// <param name="nLength">
		/// The number of screen buffer character cells from which to read.
		/// </param>
		/// <param name="dwReadCoord">
		/// The coordinates of the first cell in the console screen buffer from which to
		/// read, in characters. The X member of the <see cref="COORD"/> structure is the
		/// column, and the Y member is the row.
		/// </param>
		/// <param name="lpNumberOfCharsRead">
		/// A pointer to a variable that receives the number of characters actually read.
		/// </param>
		/// <returns>Function success.</returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool ReadConsoleOutputCharacter(
			[In] IntPtr hConsoleOutput,
			[Out] StringBuilder lpCharacter,
			[In] uint nLength,
			[In] COORD dwReadCoord,
			[Out] out uint lpNumberOfCharsRead);
		/// <summary>
		/// Moves a block of data in a screen buffer. The effects of the move can be limited
		/// by specifying a clipping rectangle, so the contents of the console screen buffer
		/// outside the clipping rectangle are unchanged.
		/// </summary>
		/// <param name="hConsoleOutput">
		/// A handle to the console screen buffer. The handle must have the GENERIC_READ
		/// access right.
		/// </param>
		/// <param name="lpScrollRectangle">
		/// A pointer to a <see cref="SMALL_RECT"/> structure whose members specify the
		/// upper-left and lower-right coordinates of the console screen buffer rectangle
		/// to be moved.
		/// </param>
		/// <param name="lpClipRectangle">
		/// A pointer to a <see cref="SMALL_RECT"/> structure whose members specify the
		/// upper-left and lower-right coordinates of the console screen buffer rectangle
		/// that is affected by the scrolling. This pointer can be <see cref="NULL"/>.
		/// </param>
		/// <param name="dwDestinationOrigin">
		/// A <see cref="COORD"/> structure that specifies the upper-left corner of the new
		/// location of the <paramref name="lpScrollRectangle"/> contents, in characters.
		/// </param>
		/// <param name="lpFill">
		/// A pointer to a <see cref="CHAR_INFO"/> structure that specifies the character
		/// and color attributes to be used in filling the cells within the intersection
		/// of <paramref name="lpScrollRectangle"/> and <paramref name="lpClipRectangle"/>
		/// that were left empty as a result of the move.
		/// </param>
		/// <returns>Function success.</returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool ScrollConsoleScreenBuffer(
			[In] IntPtr hConsoleOutput,
			[In] ref SMALL_RECT lpScrollRectangle,
			[In, Optional] IntPtr lpClipRectangle,
			[In] COORD dwDestinationOrigin,
			[In] ref CHAR_INFO lpFill);
		/// <summary>
		/// Sets the cursor position in the specified console screen buffer.
		/// </summary>
		/// <param name="hConsoleOutput">
		/// A handle to the console screen buffer. The handle must have the GENERIC_READ
		/// access right.
		/// </param>
		/// <param name="dwCursorPosition">
		/// A <see cref="COORD"/> structure that specifies the new cursor position, in
		/// characters. The coordinates are the column and row of a screen buffer character
		/// cell. The coordinates must be within the boundaries of the console screen buffer.
		/// </param>
		/// <returns>Function success.</returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool SetConsoleCursorPosition(
			[In] IntPtr hConsoleOutput,
			[In] COORD dwCursorPosition);
		/// <summary>
		/// Changes the size of the specified console screen buffer.
		/// </summary>
		/// <param name="hConsoleOutput">
		/// A handle to the console screen buffer. The handle must have the GENERIC_READ
		/// access right.
		/// </param>
		/// <param name="dwSize">
		/// A <see cref="COORD"/> structure that specifies the new size of the console
		/// screen buffer, in character rows and columns. The specified width and height
		/// cannot be less than the width and height of the console screen buffer's window.
		/// The specified dimensions also cannot be less than the minimum size allowed by
		/// the system. This minimum depends on the current font size for the console
		/// (selected by the user) and the <see cref="SM_CXMIN"/> and <see cref="SM_CYMIN"/>
		/// values returned by the <see cref="GetSystemMetrics"/> function.
		/// </param>
		/// <returns>Function success.</returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool SetConsoleScreenBufferSize(
			[In] IntPtr hConsoleOutput,
			[In] COORD dwSize);
		/// <summary>
		/// Retrieves the specified system metric or system configuration setting. Note that
		/// all dimensions retrieved by GetSystemMetrics are in pixels.
		/// </summary>
		/// <param name="nIndex">
		/// The system metric or configuration setting to be retrieved. This parameter can
		/// be one of the SM_* values. Note that all SM_CX* values are widths and all SM_CY*
		/// values are heights. Also note that all settings designed to return Boolean
		/// data represent TRUE as any nonzero value, and FALSE as a zero value.
		/// </param>
		/// <returns>
		/// If the function succeeds, the return value is the requested system metric or
		/// configuration setting
		/// </returns>
		[DllImport("user32", SetLastError = true)]
		public static extern bool GetSystemMetrics(
			[In] uint nIndex);
		/// <summary>
		/// Sets the attributes of characters written to the console screen buffer by the
		/// <see cref="WriteConsole"/> function, or echoed by the <see cref="ReadConsole"/>
		/// function. This function affects text written after the function call.
		/// </summary>
		/// <param name="hConsoleOutput">
		/// A handle to the console screen buffer. The handle must have the GENERIC_READ
		/// access right. 
		/// </param>
		/// <param name="wAttributes">
		/// The character attributes.
		/// </param>
		/// <returns>Function success.</returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool SetConsoleTextAttribute(
			[In] IntPtr hConsoleOutput,
			[In] ushort wAttributes);
		/// <summary>
		/// Sets the title for the current console window.
		/// </summary>
		/// <param name="lpConsoleTitle">
		/// The string to be displayed in the title bar of the console window. The total
		/// size must be less than 64K.
		/// </param>
		/// <returns>Function success.</returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool SetConsoleTitle(
			[In] string lpConsoleTitle);
		/// <summary>
		/// Writes data directly to the console input buffer.
		/// </summary>
		/// <param name="hConsoleInput">
		/// A handle to the console input buffer. The handle must have the GENERIC_WRITE
		/// access right. 
		/// </param>
		/// <param name="lpBuffer">
		/// A pointer to an array of <see cref="INPUT_RECORD"/> structures that contain
		/// data to be written to the input buffer.
		/// </param>
		/// <param name="nLength">
		/// The number of input records to be written.
		/// </param>
		/// <param name="lpNumberOfEventsWritten">
		/// A pointer to a variable that receives the number of input records actually written.
		/// </param>
		/// <returns>Function success.</returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool WriteConsoleInput(
			[In] IntPtr hConsoleInput,
			[MarshalAs(UnmanagedType.LPArray), In] INPUT_RECORD[] lpBuffer,
			[In] uint nLength,
			[Out] out uint lpNumberOfEventsWritten);
		/// <summary>
		/// Writes character and color attribute data to a specified rectangular block of
		/// character cells in a console screen buffer. The data to be written is taken from
		/// a correspondingly sized rectangular block at a specified location in the source
		/// buffer.
		/// </summary>
		/// <param name="hConsoleOutput">
		/// A handle to the console screen buffer. The handle must have the GENERIC_WRITE
		/// access right.
		/// </param>
		/// <param name="lpBuffer">
		/// The data to be written to the console screen buffer. This pointer is treated as
		/// the origin of a two-dimensional array of <see cref="CHAR_INFO"/> structures whose
		/// size is specified by the <paramref name="dwBufferSize"/> parameter.
		/// </param>
		/// <param name="dwBufferSize">
		/// The size of the buffer pointed to by the <paramref name="lpBuffer"/> parameter,
		/// in character cells. The X member of the <see cref="COORD"/> structure is the
		/// number of columns; the Y member is the number of rows.
		/// </param>
		/// <param name="dwBufferCoord">
		/// The coordinates of the upper-left cell in the buffer pointed to by the
		/// <paramref name="lpBuffer"/> parameter. The X member of the <see cref="COORD"/>
		/// structure is the column, and the Y member is the row.
		/// </param>
		/// <param name="lpWriteRegion">
		/// A pointer to a <see cref="SMALL_RECT"/> structure. On input, the structure
		/// members specify the upper-left and lower-right coordinates of the console
		/// screen buffer rectangle to write to. On output, the structure members specify
		/// the actual rectangle that was used.
		/// </param>
		/// <returns>Function success.</returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool WriteConsoleOutput(
			[In] IntPtr hConsoleOutput,
			[MarshalAs(UnmanagedType.LPArray), In] CHAR_INFO[] lpBuffer,
			[In] COORD dwBufferSize,
			[In] COORD dwBufferCoord,
			[In, Out] ref SMALL_RECT lpWriteRegion);
		/// <summary>
		/// Copies a number of character attributes to consecutive cells of a console screen
		/// buffer, beginning at a specified location.
		/// </summary>
		/// <param name="hConsoleOutput">
		/// A handle to the console screen buffer. The handle must have the GENERIC_WRITE
		/// access right.
		/// </param>
		/// <param name="lpAttribute">
		/// The attributes to be used when writing to the console screen buffer.
		/// </param>
		/// <param name="nLength">
		/// The number of screen buffer character cells to which the attributes will be copied.
		/// </param>
		/// <param name="dwWriteCoord">
		/// A <see cref="COORD"/> structure that specifies the character coordinates of the
		/// first cell in the console screen buffer to which the attributes will be written.
		/// </param>
		/// <param name="lpNumberOfAttrsWritten">
		/// A pointer to a variable that receives the number of attributes actually written
		/// to the console screen buffer.
		/// </param>
		/// <returns>Function success.</returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool WriteConsoleOutputAttribute(
			[In] IntPtr hConsoleOutput,
			[MarshalAs(UnmanagedType.LPArray), In] ushort[] lpAttribute,
			[In] uint nLength,
			[In] COORD dwWriteCoord,
			[Out] out uint lpNumberOfAttrsWritten);
		/// <summary>
		/// Copies a number of characters to consecutive cells of a console screen buffer,
		/// beginning at a specified location.
		/// </summary>
		/// <param name="hConsoleOutput">
		/// A handle to the console screen buffer. The handle must have the GENERIC_WRITE
		/// access right.
		/// </param>
		/// <param name="lpCharacter">
		/// The characters to be written to the console screen buffer.
		/// </param>
		/// <param name="nLength">
		/// The number of characters to be written.
		/// </param>
		/// <param name="dwWriteCoord">
		/// A <see cref="COORD"/> structure that specifies the character coordinates of the
		/// first cell in the console screen buffer to which characters will be written.
		/// </param>
		/// <param name="lpNumberOfCharsWritten">
		/// A pointer to a variable that receives the number of characters actually written.
		/// </param>
		/// <returns>Function success.</returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool WriteConsoleOutputCharacter(
			[In] IntPtr hConsoleOutput,
			[In] string lpCharacter,
			[In] uint nLength,
			[In] COORD dwWriteCoord,
			[Out] out uint lpNumberOfCharsWritten);
		/// <summary>
		/// Sets the current size and position of a console screen buffer's window.
		/// </summary>
		/// <param name="hConsoleOutput">
		/// A handle to the console screen buffer. The handle must have the GENERIC_READ
		/// access right.
		/// </param>
		/// <param name="bAbsolute">
		/// If this parameter is TRUE, the coordinates specify the new upper-left and
		/// lower-right corners of the window. If it is FALSE, the coordinates are relative
		/// to the current window-corner coordinates.
		/// </param>
		/// <param name="lpConsoleWindow">
		/// A pointer to a <see cref="SMALL_RECT"/> structure that specifies the new
		/// upper-left and lower-right corners of the window.
		/// </param>
		/// <returns></returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool SetConsoleWindowInfo(
			[In] IntPtr hConsoleOutput,
			[In] bool bAbsolute,
			[In] ref SMALL_RECT lpConsoleWindow);
		/// <summary>
		/// Retrieves the number of unread input records in the console's input buffer.
		/// </summary>
		/// <param name="hConsoleInput">
		/// A handle to the console input buffer. The handle must have the GENERIC_READ
		/// access right.
		/// </param>
		/// <param name="lpcNumberOfEvents">
		/// A pointer to a variable that receives the number of unread input records in
		/// the console's input buffer.
		/// </param>
		/// <returns>Function success.</returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool GetNumberOfConsoleInputEvents(
			[In] IntPtr hConsoleInput,
			[Out] out uint lpcNumberOfEvents);
		/// <summary>
		/// Sets the input mode of a console's input buffer or the output mode of a console
		/// screen buffer.
		/// </summary>
		/// <param name="hConsoleHandle">
		/// A handle to the console input buffer or a console screen buffer. The handle must
		/// have the GENERIC_READ access right.
		/// </param>
		/// <param name="dwMode">
		/// The input or output mode to be set. If the <paramref name="hConsoleHandle"/>
		/// parameter is an input handle, the mode can be one or more of the ENABLE_* values.
		/// When a console is created, all input modes except
		/// <see cref="ENABLE_WINDOW_INPUT"/> are enabled by default.
		/// </param>
		/// <returns>Function success.</returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool SetConsoleMode(
			[In] IntPtr hConsoleHandle,
			[In] uint dwMode);
		/// <summary>
		/// Retrieves the current input mode of a console's input buffer or the current
		/// output mode of a console screen buffer.
		/// </summary>
		/// <param name="hConsoleHandle">
		/// A handle to the console input buffer or the console screen buffer. The handle
		/// must have the GENERIC_READ access right.
		/// </param>
		/// <param name="lpMode">
		/// A pointer to a variable that receives the current mode of the specified buffer.
		/// If the <paramref name="hConsoleHandle"/> parameter is an input handle, the
		/// mode can be one or more of the ENABLE_* values. When a console is created, all
		/// input modes except <see cref="ENABLE_WINDOW_INPUT"/> are enabled by default.
		/// </param>
		/// <returns></returns>
		[DllImport("kernel32", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool GetConsoleMode(
			[In] IntPtr hConsoleHandle,
			[Out] out uint lpMode);
		#endregion

		#region Structures
		/// <summary>
		/// Contains information about the console cursor.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct CONSOLE_CURSOR_INFO {
			/// <summary>
			/// The percentage of the character cell that is filled by the cursor.
			/// This value is between 1 and 100. The cursor appearance varies, ranging from
			/// completely filling the cell to showing up as a horizontal line at the bottom
			/// of the cell.
			/// </summary>
			/// <remarks>
			/// While the <see cref="dwSize"/> value is normally between 1
			/// and 100, under some circumstances a value outside of that range might be
			/// returned. For example, if CursorSize is set to 0 in the registry, the
			/// <see cref="dwSize"/> value returned would be 0.
			/// </remarks>
			public uint dwSize;
			/// <summary>
			/// The visibility of the cursor. If the cursor is visible, this member is TRUE.
			/// </summary>
			public bool bVisible;
		}
		/// <summary>
		/// Defines the coordinates of the upper left and lower right corners of a rectangle.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct SMALL_RECT {
			/// <summary>
			/// The x-coordinate of the upper left corner of the rectangle.
			/// </summary>
			public short Left;
			/// <summary>
			/// The y-coordinate of the upper left corner of the rectangle.
			/// </summary>
			public short Top;
			/// <summary>
			/// The x-coordinate of the lower right corner of the rectangle.
			/// </summary>
			public short Right;
			/// <summary>
			/// The y-coordinate of the lower right corner of the rectangle.
			/// </summary>
			public short Bottom;
		}
		/// <summary>
		/// Defines the coordinates of a character cell in a console screen buffer. 
		/// The origin of the coordinate system (0,0) is at the top, left cell of the buffer.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct COORD {
			/// <summary>
			/// The horizontal coordinate or column value. The units depend on the function call.
			/// </summary>
			public short X;
			/// <summary>
			/// The vertical coordinate or column value. The units depend on the function call.
			/// </summary>
			public short Y;
		}
		/// <summary>
		/// Contains information about a console screen buffer.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct CONSOLE_SCREEN_BUFFER_INFO {
			/// <summary>
			/// A <see cref="COORD"/> structure that contains the size of the console screen buffer,
			/// in character columns and rows.
			/// </summary>
			public COORD dwSize;
			/// <summary>
			/// A <see cref="COORD"/> structure that contains the column and row coordinates of the
			/// cursor in the console screen buffer.
			/// </summary>
			public COORD dwCursorPosition;
			/// <summary>
			/// The attributes of the characters written to a screen buffer by the
			/// <see cref="WriteConsole"/> function, or echoed to a screen buffer by the
			/// <see cref="ReadConsole"/> function.
			/// </summary>
			public ushort wAttributes;
			/// <summary>
			/// A <see cref="SMALL_RECT"/> structure that contains the console screen buffer
			/// coordinates of the upper-left and lower-right corners of the display window.
			/// </summary>
			public SMALL_RECT srWindow;
			/// <summary>
			/// A <see cref="COORD"/> structure that contains the maximum size of the console
			/// window, in character columns and rows, given the current screen buffer size
			/// and font and the screen size.
			/// </summary>
			public COORD dwMaximumWindowSize;
		}
		/// <summary>
		/// Contains information for a console selection.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct CONSOLE_SELECTION_INFO {
			/// <summary>
			/// The selection indicator. This member can be one or more of CONSOLE_* values.
			/// </summary>
			public uint dwFlags;
			/// <summary>
			/// A <see cref="COORD"/> structure that specifies the selection anchor, in characters.
			/// </summary>
			public COORD dwSelectionAnchor;
			/// <summary>
			/// A <see cref="SMALL_RECT"/> structure that specifies the selection rectangle.
			/// </summary>
			public SMALL_RECT srSelection;
		}
		/// <summary>
		/// Describes an input event in the console input buffer. These records can be read
		/// from the input buffer by using the <see cref="ReadConsoleInput"/> or
		/// <see cref="PeekConsoleInput"/> function, or written to the input buffer by using
		/// the <see cref="WriteConsoleInput"/> function.
		/// </summary>
		[StructLayout(LayoutKind.Explicit)]
		public struct INPUT_RECORD {
			/// <summary>
			/// A handle to the type of input event and the event record stored in the Event
			/// member. This member can be one of the *_EVENT values.
			/// </summary>
			[FieldOffset(0)]
			public ushort EventType;
			/// <summary>
			/// The <see cref="KEY_EVENT"/> information.
			/// </summary>
			[FieldOffset(4)]
			public KEY_EVENT_RECORD KeyEvent;
			/// <summary>
			/// The <see cref="MOUSE_EVENT"/> information.
			/// </summary>
			[FieldOffset(4)]
			public MOUSE_EVENT_RECORD MouseEvent;
			/// <summary>
			/// The <see cref="WINDOW_BUFFER_SIZE_EVENT"/> information.
			/// </summary>
			[FieldOffset(4)]
			public WINDOW_BUFFER_SIZE_RECORD WindowBufferSizeEvent;
			/// <summary>
			/// The <see cref="MENU_EVENT"/> information.
			/// </summary>
			[FieldOffset(4)]
			public MENU_EVENT_RECORD MenuEvent;
			/// <summary>
			/// The <see cref="FOCUS_EVENT"/> information.
			/// </summary>
			[FieldOffset(4)]
			public FOCUS_EVENT_RECORD FocusEvent;
		}
		/// <summary>
		/// Describes a focus event in a console <see cref="INPUT_RECORD"/> structure.
		/// These events are used internally and should be ignored.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct FOCUS_EVENT_RECORD {
			/// <summary>
			/// Reserved.
			/// </summary>
			public bool bSetFocus;
		}
		/// <summary>
		/// Describes a keyboard input event in a console <see cref="INPUT_RECORD"/> structure.
		/// </summary>
		[StructLayout(LayoutKind.Explicit)]
		public struct KEY_EVENT_RECORD {
			/// <summary>
			/// If the key is pressed, this member is TRUE. Otherwise, this member is FALSE
			/// (the key is released).
			/// </summary>
			[FieldOffset(0)]
			public bool bKeyDown;
			/// <summary>
			/// The repeat count, which indicates that a key is being held down. For example,
			/// when a key is held down, you might get five events with this member equal to
			/// 1, one event with this member equal to 5, or multiple events with this member
			/// greater than or equal to 1.
			/// </summary>
			[FieldOffset(4)]
			public ushort wRepeatCount;
			/// <summary>
			/// A virtual-key code that identifies the given key in a device-independent
			/// manner.
			/// </summary>
			[FieldOffset(6)]
			public ushort wVirtualKeyCode;
			/// <summary>
			/// The virtual scan code of the given key that represents the device-dependent
			/// value generated by the keyboard hardware.
			/// </summary>
			[FieldOffset(8)]
			public ushort wVirtualScanCode;
			/// <summary>Translated Unicode character.</summary>
			[FieldOffset(10)]
			public char UnicodeChar;
			/// <summary>Translated ASCII character.</summary>
			[FieldOffset(10)]
			public byte AsciiChar;
			/// <summary>
			/// The state of the control keys. This member can be one or more of the
			/// following values:
			/// <see cref="CAPSLOCK_ON"/> <see cref="ENHANCED_KEY"/>
			/// <see cref="LEFT_ALT_PRESSED"/> <see cref="LEFT_CTRL_PRESSED"/>
			/// <see cref="NUMLOCK_ON"/> <see cref="RIGHT_ALT_PRESSED"/>
			/// <see cref="RIGHT_CTRL_PRESSED"/> <see cref="SCROLLLOCK_ON"/>
			/// <see cref="SHIFT_PRESSED"/>
			/// </summary>
			[FieldOffset(12)]
			public uint dwControlKeyState;
		}
		/// <summary>
		/// Describes a menu event in a console <see cref="INPUT_RECORD"/> structure.
		/// These events are used internally and should be ignored.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct MENU_EVENT_RECORD {
			/// <summary>
			/// Reserved.
			/// </summary>
			public uint dwCommandId;
		}
		/// <summary>
		/// Describes a mouse input event in a console <see cref="INPUT_RECORD"/> structure.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct MOUSE_EVENT_RECORD {
			/// <summary>
			/// A <see cref="COORD"/> structure that contains the location of the cursor,
			/// in terms of the console screen buffer's character-cell coordinates.
			/// </summary>
			public COORD dwMousePosition;
			/// <summary>
			/// The status of the mouse buttons. The least significant bit corresponds to the
			/// leftmost mouse button. The next least significant bit corresponds to the
			/// rightmost mouse button. The next bit indicates the next-to-leftmost mouse
			/// button. The bits then correspond left to right to the mouse buttons. A bit is
			/// 1 if the button was pressed. The *_BUTTON_PRESSED constants are defined for
			/// the first five mouse buttons.
			/// </summary>
			public uint dwButtonState;
			/// <summary>
			/// The state of the control keys. This member can be one or more of the
			/// following values:
			/// <see cref="CAPSLOCK_ON"/> <see cref="ENHANCED_KEY"/>
			/// <see cref="LEFT_ALT_PRESSED"/> <see cref="LEFT_CTRL_PRESSED"/>
			/// <see cref="NUMLOCK_ON"/> <see cref="RIGHT_ALT_PRESSED"/>
			/// <see cref="RIGHT_CTRL_PRESSED"/> <see cref="SCROLLLOCK_ON"/>
			/// <see cref="SHIFT_PRESSED"/>
			/// </summary>
			public uint dwControlKeyState;
			/// <summary>
			/// The type of mouse event. If this value is zero, it indicates a mouse button
			/// being pressed or released. Otherwise, this member is one of the following
			/// values: <see cref="DOUBLE_CLICK"/> <see cref="MOUSE_HWHEELED"/>
			/// <see cref="MOUSE_MOVED"/> <see cref="MOUSE_WHEELED"/>
			/// </summary>
			public uint dwEventFlags;
		}
		/// <summary>
		/// Describes a change in the size of the console screen buffer.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct WINDOW_BUFFER_SIZE_RECORD {
			/// <summary>
			/// A <see cref="COORD"/> structure that contains the size of the console screen
			/// buffer, in character cell columns and rows.
			/// </summary>
			public COORD dwSize;
		}
		/// <summary>
		/// Contains information for a console read operation.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct CONSOLE_READCONSOLE_CONTROL {
			/// <summary>
			/// The size of the structure.
			/// </summary>
			public ulong nLength;
			/// <summary>
			/// The number of characters to skip (and thus preserve) before writing newly
			/// read input in the buffer passed to the ReadConsole function. This value must
			/// be less than the nNumberOfCharsToRead parameter of the
			/// <see cref="ReadConsole"/> function.
			/// </summary>
			public ulong nInitialChars;
			/// <summary>
			/// A user-defined control character used to signal that the read is complete.
			/// </summary>
			public ulong dwCtrlWakeupMask;
			/// <summary>
			/// The state of the control keys. This member can be one or more of the
			/// following values:
			/// <see cref="CAPSLOCK_ON"/> <see cref="ENHANCED_KEY"/>
			/// <see cref="LEFT_ALT_PRESSED"/> <see cref="LEFT_CTRL_PRESSED"/>
			/// <see cref="NUMLOCK_ON"/> <see cref="RIGHT_ALT_PRESSED"/>
			/// <see cref="RIGHT_CTRL_PRESSED"/> <see cref="SCROLLLOCK_ON"/>
			/// <see cref="SHIFT_PRESSED"/>
			/// </summary>
			public ulong dwControlKeyState;
		}
		/// <summary>
		/// Specifies a Unicode or ANSI character and its attributes.
		/// This structure is used by console functions to read from and write to a
		/// console screen buffer.
		/// </summary>
		[StructLayout(LayoutKind.Explicit)]
		public struct CHAR_INFO {
			/// <summary>
			/// Unicode character of a screen buffer character cell.
			/// </summary>
			[FieldOffset(0)]
			public char UnicodeChar;
			/// <summary>
			/// ANSI character of a screen buffer character cell.
			/// </summary>
			[FieldOffset(0)]
			public byte AsciiChar;
			/// <summary>
			/// The character attributes. This member can be zero or any combination of the
			/// FOREGROUND_*, BACKGROUND_* and COMMON_* values.
			/// </summary>
			[FieldOffset(2)]
			public ushort Attributes;
		}
		#endregion
	}
}
[Flags]
public enum ConsoleOutputMode {
	ProcessedOutput = (int) ENABLE_PROCESSED_OUTPUT,
	WrapAtEolOutput = (int) ENABLE_WRAP_AT_EOL_OUTPUT,
	VirtualTerminalProcessing = (int) ENABLE_VIRTUAL_TERMINAL_PROCESSING,
	NewlineAutoReturn = (int) ENABLE_NEWLINE_AUTO_RETURN,
	LvbGridWorldwide = (int) ENABLE_LVB_GRID_WORLDWIDE
}
[Flags]
public enum ConsoleInputMode {
	ExtendedFlags = (int) ENABLE_EXTENDED_FLAGS,
	EchoInput = (int) ENABLE_ECHO_INPUT,
	InsertMode = (int) ENABLE_INSERT_MODE,
	LineInput = (int) ENABLE_LINE_INPUT,
	MouseInput = (int) ENABLE_MOUSE_INPUT,
	ProcessedInput = (int) ENABLE_PROCESSED_INPUT,
	QuickEditMode = (int) ENABLE_QUICK_EDIT_MODE,
	WindowInput = (int) ENABLE_WINDOW_INPUT,
	VirtualTerminalInput = (int) ENABLE_VIRTUAL_TERMINAL_INPUT
}
public interface IInputEvent { }
[Flags]
public enum ControlKeyState {
	CapsLock = (int) CAPSLOCK_ON,
	Enhanced = (int) ENHANCED_KEY,
	LeftAlt = (int) LEFT_ALT_PRESSED,
	LeftCtrl = (int) LEFT_CTRL_PRESSED,
	NumLock = (int) NUMLOCK_ON,
	RightAlt = (int) RIGHT_ALT_PRESSED,
	RightCtrl = (int) RIGHT_CTRL_PRESSED,
	ScrollLock = (int) SCROLLLOCK_ON,
	Shift = (int) SHIFT_PRESSED
}
[Flags]
public enum MouseInputEventType {
	Click = 0,
	DoubleClick = (int) DOUBLE_CLICK,
	HorizontalWheel = (int) MOUSE_HWHEELED,
	Moved = (int) MOUSE_MOVED,
	VerticalWheel = (int) MOUSE_WHEELED
}
public class KeyInputEvent : IInputEvent {
	public bool IsKeyDown { get; }
	public int RepeatCount { get; }
	public uint KeyCode { get; }
	public uint ScanCode { get; }
	public char Char { get; }
	public ControlKeyState ControlKeyState { get; }
	public KeyInputEvent(KEY_EVENT_RECORD native) {
		IsKeyDown = native.bKeyDown;
		RepeatCount = native.wRepeatCount;
		KeyCode = native.wVirtualKeyCode;
		ScanCode = native.wVirtualScanCode;
		Char = native.UnicodeChar;
		ControlKeyState = (ControlKeyState) native.dwControlKeyState;
	}
}
public class MouseInputEvent : IInputEvent {
	public int MouseX { get; }
	public int MouseY { get; }
	public bool LeftButton { get; }
	public bool MiddleButton { get; }
	public bool RightButton { get; }
	public bool XButton1 { get; }
	public bool XButton2 { get; }
	public ControlKeyState ControlKeyState { get; }
	public MouseInputEventType Type { get; }
	public MouseInputEvent(MOUSE_EVENT_RECORD native) {
		MouseX = native.dwMousePosition.X;
		MouseY = native.dwMousePosition.Y;
		LeftButton = (native.dwButtonState & FROM_LEFT_1ST_BUTTON_PRESSED) > 0;
		MiddleButton = (native.dwButtonState & FROM_LEFT_2ND_BUTTON_PRESSED) > 0;
		RightButton = (native.dwButtonState & RIGHTMOST_BUTTON_PRESSED) > 0;
		XButton1 = (native.dwButtonState & FROM_LEFT_3RD_BUTTON_PRESSED) > 0;
		XButton2 = (native.dwButtonState & FROM_LEFT_4TH_BUTTON_PRESSED) > 0;
		ControlKeyState = (ControlKeyState) native.dwControlKeyState;
		Type = (MouseInputEventType) native.dwEventFlags;
	}
}
public class WindowBufferSizeInputEvent : IInputEvent {
	public int Width { get; }
	public int Height { get; }
	public WindowBufferSizeInputEvent(WINDOW_BUFFER_SIZE_RECORD native) {
		Width = native.dwSize.X;
		Height = native.dwSize.Y;
	}
}
