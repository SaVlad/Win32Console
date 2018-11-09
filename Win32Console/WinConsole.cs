using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using static Win32Console.NativeApi;

/// <summary>
/// Managed interface for unmanaged win32 console functions
/// </summary>
public static class Win32Console {
	/// <summary>
	/// Unmanaged console screen buffer info structure for internal use
	/// </summary>
	private static CONSOLE_SCREEN_BUFFER_INFO ScreenBufferInfo {
		get {
			if(!GetConsoleScreenBufferInfo(OutputHandle, out CONSOLE_SCREEN_BUFFER_INFO csbi))
				ThrowWin32("Failed to get console buffer info");
			return csbi;
		}
	}
	/// <summary>
	/// Stack for pushing/popping console output modes
	/// </summary>
	private static Stack<ConsoleOutputMode> StoredModesOutput { get; } = new Stack<ConsoleOutputMode>();
	/// <summary>
	/// Stack for pushing/popping console input modes
	/// </summary>
	private static Stack<ConsoleInputMode> StoredModesInput { get; } = new Stack<ConsoleInputMode>();
	/// <summary>
	/// Unmanaged pointer to standard input device (stdin)
	/// </summary>
	public static IntPtr InputHandle { get; private set; } = NULL;
	/// <summary>
	/// Unmanaged pointer to standard output device (stdout)
	/// </summary>
	public static IntPtr OutputHandle { get; private set; } = NULL;
	/// <summary>
	/// True, if <see cref="OutputHandle"/> is available to use
	/// </summary>
	public static bool OutputAvailable => OutputHandle != NULL;
	/// <summary>
	/// True, if <see cref="InputHandle"/> is available to use
	/// </summary>
	public static bool InputAvailable => InputHandle != NULL;
	/// <summary>
	/// Console cursor horizontal position in character cells
	/// </summary>
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
	/// <summary>
	/// Console cursor vertical position in character cells
	/// </summary>
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
	/// <summary>
	/// Console screen buffer current width in character cells
	/// </summary>
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
	/// <summary>
	/// Console screen buffer current height in character cells
	/// </summary>
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
	/// <summary>
	/// Console window width current width in character cells
	/// </summary>
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
	/// <summary>
	/// Console window height current width in character cells
	/// </summary>
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
	/// <summary>
	/// Current default console text foreground color
	/// </summary>
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
	/// <summary>
	/// Current default console text background color
	/// </summary>
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
	/// <summary>
	/// Current console window title text
	/// </summary>
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
	/// <summary>
	/// True, if blinking cursor is visible
	/// </summary>
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
	/// <summary>
	/// Amount of input events pending.
	/// Note that this also includes possible unsupported input events.
	/// </summary>
	public static int InputEventsCount {
		get {
			if(!OutputAvailable)
				return 0;
			if(!GetNumberOfConsoleInputEvents(InputHandle, out uint c))
				ThrowWin32("Failed to get number of console input events");
			return (int) c;
		}
	}
	/// <summary>
	/// Current console output mode
	/// </summary>
	public static ConsoleOutputMode ConsoleOutputMode {
		get {
			if(!OutputAvailable)
				return 0;
			if(!GetConsoleMode(OutputHandle, out uint mode))
				ThrowWin32("Failed to get console output mode");
			return (ConsoleOutputMode) mode;
		}
	}
	/// <summary>
	/// Current console input mode
	/// </summary>
	public static ConsoleInputMode ConsoleInputMode {
		get {
			if(!OutputAvailable)
				return 0;
			if(!GetConsoleMode(InputHandle, out uint mode))
				ThrowWin32("Failed to get console input mode");
			return (ConsoleInputMode) mode;
		}
	}
	/// <summary>
	/// Initial console handles loading
	/// </summary>
	static Win32Console() {
		InputHandle = GetStdHandle(STD_INPUT_HANDLE);
		if(InputHandle == INVALID_HANDLE_VALUE)
			throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to retrieve STD_INPUT_HANDLE");
		OutputHandle = GetStdHandle(STD_OUTPUT_HANDLE);
		if(OutputHandle == INVALID_HANDLE_VALUE)
			throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to retrieve STD_OUTPUT_HANDLE");
	}

	/// <summary>
	/// Throws new <see cref="Win32Exception"/> with <paramref name="message"/> and native description.
	/// </summary>
	/// <param name="message">Custom message</param>
	private static void ThrowWin32(string message) {
		try {
			throw new Win32Exception(Marshal.GetLastWin32Error());
		} catch(Win32Exception exc) {
			throw new Win32Exception($"{message}. {exc.Message}");
		}
	}
	/// <summary>
	/// Return unmanaged size of <typeparamref name="T"/> structure.
	/// </summary>
	/// <typeparam name="T">Structure type to measure</typeparam>
	/// <returns>Unmanaged size of <typeparamref name="T"/> structure</returns>
	private static int SizeOf<T>() where T : struct => SizeOf(new T());
	/// <summary>
	/// Return unmanaged size of <typeparamref name="T"/> structure.
	/// </summary>
	/// <typeparam name="T">Structure type to measure</typeparam>
	/// <param name="obj">Instance of <typeparamref name="T"/> structure</param>
	/// <returns>Unmanaged size of <typeparamref name="T"/> structure</returns>
	private static int SizeOf<T>(T obj) where T : struct => Marshal.SizeOf(obj);
	/// <summary>
	/// Unused.
	/// Returns CtrlWakeupMask for specified characters to be used in
	/// <see cref="CONSOLE_READCONSOLE_CONTROL"/>
	/// </summary>
	/// <param name="chars">Characters to include in mask</param>
	/// <returns>CtrlWakeupMask for <see cref="CONSOLE_READCONSOLE_CONTROL"/></returns>
	private static ulong BuildCtrlWakeupMask(char[] chars) {
		ulong mask = 0;
		foreach(char c in chars)
			mask |= 1UL << c;
		return mask;
	}
	/// <summary>
	/// Unused.
	/// Returns unmanaged pointer to <paramref name="obj"/> structure instance.
	/// Pointer must be freed with <see cref="FreeStructUnmanaged{T}(IntPtr)"/> later.
	/// </summary>
	/// <typeparam name="T">Structure to marshal</typeparam>
	/// <param name="obj">Instance to marshal</param>
	/// <returns>Unmanaged pointer for <paramref name="obj"/></returns>
	private static IntPtr MallocStructUnmanaged<T>(T obj) where T : struct {
		IntPtr ptr = Marshal.AllocHGlobal(SizeOf(obj));
		Marshal.StructureToPtr(obj, ptr, false);
		return ptr;
	}
	/// <summary>
	/// Unused.
	/// Releases unmanaged pointer created in <see cref="MallocStructUnmanaged{T}(T)"/>
	/// </summary>
	/// <typeparam name="T">Structure type <paramref name="ptr"/> points to</typeparam>
	/// <param name="ptr">Unmanaged pointer to release</param>
	private static void FreeStructUnmanaged<T>(IntPtr ptr) => Marshal.DestroyStructure(ptr, typeof(T));
	/// <summary>
	/// Returns next <see cref="INPUT_RECORD"/> available in system queue.
	/// Note that this function assumes that queue is not empty.
	/// </summary>
	/// <returns>Next available <see cref="INPUT_RECORD"/></returns>
	private static INPUT_RECORD NextInputRecord() {
		INPUT_RECORD[] ir = new[] { new INPUT_RECORD() };
		if(!ReadConsoleInput(InputHandle, ir, 1, out uint l))
			ThrowWin32("Failed to read console input");
		return ir[0];
	}
	/// <summary>
	/// Parses native console mode to managed modes
	/// </summary>
	/// <param name="mode">Native console mode</param>
	/// <param name="o">Managed console output mode</param>
	/// <param name="i">Managed cosnole input mode</param>
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
	/// <summary>
	/// Runs function with timeout. If <paramref name="timeout"/> is zero, waits indefinitely.
	/// If timeout occurs and <paramref name="abort"/> is set, then function is aborted.
	/// </summary>
	/// <param name="timeout">Amount of ms to wait for function to finish</param>
	/// <param name="ts">Function to run</param>
	/// <param name="abort">If true, then function is aborted on timeout</param>
	/// <returns>True, is function timed out</returns>
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
	/// <summary>
	/// Unused. For debugging purposes.
	/// Translates <see cref="FlagsAttribute"/> enum <typeparamref name="T"/> value to string
	/// </summary>
	/// <typeparam name="T">Enum type</typeparam>
	/// <param name="value">Integer representation of <typeparamref name="T"/></param>
	/// <returns>String representation of <paramref name="value"/></returns>
	private static string Debug_TranslateEnum<T>(int value) {
		if(!typeof(T).IsEnum)
			return "Not enum";
		List<string> names = new List<string>();
		var vals = Enum.GetValues(typeof(T)).Cast<int>();
		foreach(int val in vals)
			if((value & val) == val)
				names.Add(Enum.GetName(typeof(T), val));
		return $"{typeof(T).Name}{{{string.Join(", ", names)}}}";
	}

	/// <summary>
	/// Allocates console window. If current application already has one, uses it.
	/// </summary>
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
	/// <summary>
	/// Writes string representation of <paramref name="o"/> to console output
	/// </summary>
	/// <param name="o">Object to write to console output</param>
	/// <returns>Amount of characters written</returns>
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
	/// <summary>
	/// Writes formatted string to console output
	/// </summary>
	/// <param name="s">Format string</param>
	/// <param name="o">Format parameters</param>
	/// <returns>Amount of characters written</returns>
	public static int WriteFormat(string s = null, params object[] o) => Write(string.Format(s ?? "", o));
	/// <summary>
	/// Writes string representation of <paramref name="o"/> to console output
	/// and ends it with a newline.
	/// </summary>
	/// <param name="o">Object to write to console output</param>
	/// <returns>Amount of characters written</returns>
	public static int WriteLine(object o = null) => Write((o?.ToString() ?? "") + "\r\n");
	/// <summary>
	/// Writes formatted string to console output and ends it with a newline.
	/// </summary>
	/// <param name="s">Format string</param>
	/// <param name="o">Format parameters</param>
	/// <returns>Amount of characters written</returns>
	public static int WriteLineFormat(string s = null, params object[] o) => WriteLine(string.Format(s ?? "", o));
	/// <summary>
	/// Writes character to the current cell without advancing cursor position
	/// </summary>
	/// <param name="c">Character to write</param>
	public static void PutChar(char c) => PutChar(CursorX, CursorY, c);
	/// <summary>
	/// Writes character to specified cell without moving cursor
	/// </summary>
	/// <param name="x">Cell column</param>
	/// <param name="y">Cell row</param>
	/// <param name="c">Character to write</param>
	public static void PutChar(short x, short y, char c) {
		if(!OutputAvailable)
			return;
		if(!WriteConsoleOutputCharacter(OutputHandle, c.ToString(), 1, new COORD { X = x, Y = y }, out uint l))
			ThrowWin32("Failed to write console output character");
	}
	/// <summary>
	/// Writes string starting from current cell without advancing cursor position.
	/// Note that this function support control characters: \r, \n, \b, \f, \0
	/// </summary>
	/// <param name="s">String to write</param>
	public static void PutString(string s) => PutString(CursorX, CursorY, s ?? "");
	/// <summary>
	/// Writes string starting from specified cell without moving cursor.
	/// Note that this function support control characters: \r, \n, \b, \f, \0
	/// </summary>
	/// <param name="x">Cell column</param>
	/// <param name="y">Cell row</param>
	/// <param name="s">String to write</param>
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
	/// <summary>
	/// Changes current cell's color
	/// </summary>
	/// <param name="fore">Foreground color</param>
	/// <param name="back">Background color</param>
	public static void SetColor(ConsoleColor fore, ConsoleColor back) => SetColor(CursorX, CursorY, fore, back);
	/// <summary>
	/// Changes specified cell's color
	/// </summary>
	/// <param name="x">Cell column</param>
	/// <param name="y">Cell row</param>
	/// <param name="fore">Foreground color</param>
	/// <param name="back">Background color</param>
	public static void SetColor(short x, short y, ConsoleColor fore, ConsoleColor back) {
		if(!OutputAvailable)
			return;
		if(!WriteConsoleOutputAttribute(OutputHandle, new ushort[] { (ushort) ((short) fore | (short) ((short) back << 4)) }, 1, new COORD { X = x, Y = y }, out uint l))
			ThrowWin32("Failed to write console output attribute");
	}
	/// <summary>
	/// Returns character written at current cell
	/// </summary>
	/// <returns>Character at cursor</returns>
	public static char GetChar() => GetChar(CursorX, CursorY);
	/// <summary>
	/// Returns character written at specified cell
	/// </summary>
	/// <param name="x">Cell column</param>
	/// <param name="y">Cell row</param>
	/// <returns>Character at specified cell</returns>
	public static char GetChar(short x, short y) {
		if(!OutputAvailable)
			return '\0';
		StringBuilder sb = new StringBuilder();
		if(!ReadConsoleOutputCharacter(OutputHandle, sb, 1, new COORD { X = x, Y = y }, out uint l))
			ThrowWin32("Failed to read console output character");
		return sb.ToString()[0];
	}
	/// <summary>
	/// Returns string with specified length written from cursor position and after.
	/// </summary>
	/// <param name="length">Amount of characters to read</param>
	/// <returns>String written in cells</returns>
	public static string GetString(int length) => GetString(CursorX, CursorY, length);
	/// <summary>
	/// Returns string with specified length written from specified cell and after.
	/// </summary>
	/// <param name="x">Cell column</param>
	/// <param name="y">Cell row</param>
	/// <param name="length">Amount of characters to read</param>
	/// <returns>String written in cells</returns>
	public static string GetString(short x, short y, int length) {
		if(!OutputAvailable)
			return null;
		StringBuilder sb = new StringBuilder();
		if(!ReadConsoleOutputCharacter(OutputHandle, sb, (uint) length, new COORD { X = x, Y = y }, out uint l))
			ThrowWin32("Failed to read console output character");
		return sb.ToString();
	}
	/// <summary>
	/// Returns foreground color set on cursor cell
	/// </summary>
	/// <returns>Foreground color of cursor cell</returns>
	public static ConsoleColor GetForegroundColor() => GetForegroundColor(CursorX, CursorY);
	/// <summary>
	/// Returns foreground color set on specified cell
	/// </summary>
	/// <returns>Foreground color of specified cell</returns>
	public static ConsoleColor GetForegroundColor(short x, short y) {
		if(!OutputAvailable)
			return ForegroundColor;
		ushort[] attr = new ushort[1];
		if(!ReadConsoleOutputAttribute(OutputHandle, attr, 1, new COORD { X = x, Y = y }, out uint l))
			ThrowWin32("Failed to read console output attribute");
		return (ConsoleColor) (attr[0] & 0xF);
	}
	/// <summary>
	/// Returns background color set on cursor cell
	/// </summary>
	/// <returns>Background color of cursor cell</returns>
	public static ConsoleColor GetBackgroundColor() => GetBackgroundColor(CursorX, CursorY);
	/// <summary>
	/// Returns background color set on specified cell
	/// </summary>
	/// <returns>Background color of specified cell</returns>
	public static ConsoleColor GetBackgroundColor(short x, short y) {
		if(!OutputAvailable)
			return ForegroundColor;
		ushort[] attr = new ushort[1];
		if(!ReadConsoleOutputAttribute(OutputHandle, attr, 1, new COORD { X = x, Y = y }, out uint l))
			ThrowWin32("Failed to read console output attribute");
		return (ConsoleColor) ((attr[0] & 0xF0) >> 4);
	}

	/// <summary>
	/// Reads string from console input. Behaviour depends on <see cref="ConsoleInputMode"/>
	/// </summary>
	/// <returns>String read from console input</returns>
	public static string Read() {
		if(!InputAvailable)
			return null;
		StringBuilder sb = new StringBuilder();
		CONSOLE_READCONSOLE_CONTROL crcc = new CONSOLE_READCONSOLE_CONTROL {
			nLength = (ulong) SizeOf<CONSOLE_READCONSOLE_CONTROL>(),
			nInitialChars = 0,
			dwCtrlWakeupMask = 0,
			dwControlKeyState = 0
		};
		if(!ReadConsole(InputHandle, sb, 1024U, out uint l, ref crcc))
			ThrowWin32("Failed to read console");
		return sb.ToString().Substring(0, (int) l);
	}
	/// <summary>
	/// Returns next supported input event without removing it from queue or null
	/// if queue is empty, or no supported events found.
	/// Note that this function removes from queue any unsupported input events it
	/// meets on its way.
	/// </summary>
	/// <returns>Next supported input event</returns>
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
	/// <summary>
	/// Returns next supported input event removing it from queue or null,
	/// if queue is empty or no supported events found.
	/// Note that this function removes from queue any unsupported input events it
	/// meets on its way.
	/// </summary>
	/// <returns>Next supported input event</returns>
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
	/// <summary>
	/// Blocks thread until there is any input events pending until timeout occurs.
	/// If <paramref name="timeout"/> is zero, waits indefinitely.
	/// Returns True, if new event occured before timeout was hit.
	/// Note that this function does not distinguish supported and unsupported events.
	/// </summary>
	/// <param name="timeout">Timeout in ms</param>
	/// <returns></returns>
	public static bool WaitForInputEvent(int timeout) {
		if(!InputAvailable)
			return false;
		return !Wait(timeout, () => {
			while(InputEventsCount == 0)
				Thread.Sleep(100);
		}, true);
	}
	/// <summary>
	/// Returns supported input events forever or until queue is empty.
	/// Note that there is no good way of stopping it once it ran with
	/// <paramref name="infinite"/> flag set.
	/// </summary>
	/// <param name="infinite">If true, runs forever</param>
	/// <returns></returns>
	public static IEnumerable<IInputEvent> PumpInputEvents(bool infinite) {
		if(!InputAvailable)
			yield break;
		IInputEvent ev;
		if(infinite) {
			while(true) {
				WaitForInputEvent(0);
				ev = NextInputEvent();
				if(ev != null)
					yield return ev;
			}
		} else {
			while(InputEventsCount > 0) {
				ev = NextInputEvent();
				if(ev != null)
					yield return ev;
			}
		}
	}

	/// <summary>
	/// Changes console output mode to <paramref name="mode"/>
	/// </summary>
	/// <param name="mode">Console output mode to set</param>
	public static void SetConsoleOutputMode(ConsoleOutputMode mode) {
		if(!OutputAvailable)
			return;
		if(!SetConsoleMode(OutputHandle, (uint) mode))
			ThrowWin32("Failed to set console output mode");
	}
	/// <summary>
	/// Changes console input mode to <paramref name="mode"/>
	/// </summary>
	/// <param name="mode">Console input mode to set</param>
	public static void SetConsoleInputMode(ConsoleInputMode mode) {
		if(!InputAvailable)
			return;
		System.Diagnostics.Debug.WriteLine(Debug_TranslateEnum<ConsoleInputMode>((int) mode));
		if(!SetConsoleMode(InputHandle, (uint) mode))
			ThrowWin32("Failed to set console input mode");
	}
	/// <summary>
	/// Stores current console output mode to internal stack available to be popped
	/// later with <see cref="PopConsoleOutputMode"/>, changes it to specified mode
	/// and returns old one.
	/// </summary>
	/// <param name="mode">New console output mode to set</param>
	/// <returns>Old console output mode</returns>
	public static ConsoleOutputMode PushConsoleOutputMode(ConsoleOutputMode mode) {
		if(!OutputAvailable)
			return 0;
		ConsoleOutputMode old = ConsoleOutputMode;
		StoredModesOutput.Push(old);
		SetConsoleOutputMode(mode);
		return old;
	}
	/// <summary>
	/// Stores current console input mode to internal stack available to be popped
	/// later with <see cref="PopConsoleInputMode"/>, changes it to specified mode
	/// and returns old one.
	/// </summary>
	/// <param name="mode">New console input mode to set</param>
	/// <returns>Old console input mode</returns>
	public static ConsoleInputMode PushConsoleInputMode(ConsoleInputMode mode) {
		if(!InputAvailable)
			return 0;
		ConsoleInputMode old = ConsoleInputMode;
		StoredModesInput.Push(old);
		SetConsoleInputMode(mode);
		return old;
	}
	/// <summary>
	/// Retrieves stored with <see cref="PushConsoleOutputMode(ConsoleOutputMode)"/>
	/// console output mode from of of internal stack and sets it returning old one.
	/// </summary>
	/// <returns>Old console output mode</returns>
	public static ConsoleOutputMode PopConsoleOutputMode() {
		if(!OutputAvailable || StoredModesOutput.Count == 0)
			return 0;
		ConsoleOutputMode old = ConsoleOutputMode;
		SetConsoleOutputMode(StoredModesOutput.Pop());
		return old;
	}
	/// <summary>
	/// Retrieves stored with <see cref="PushConsoleInputMode(ConsoleInputMode)"/>
	/// console input mode from of of internal stack and sets it returning old one.
	/// </summary>
	/// <returns>Old console input mode</returns>
	public static ConsoleInputMode PopConsoleInputMode() {
		if(!InputAvailable || StoredModesInput.Count == 0)
			return 0;
		ConsoleInputMode old = ConsoleInputMode;
		SetConsoleInputMode(StoredModesInput.Pop());
		return old;
	}
	/// <summary>
	/// Sets exact output mode flags without changing the rest
	/// </summary>
	/// <param name="flags">Console output mode flags to set</param>
	public static void SetConsoleOutputModeFlags(ConsoleOutputMode flags) {
		if(!OutputAvailable)
			return;
		SetConsoleOutputMode(ConsoleOutputMode | flags);
	}
	/// <summary>
	/// Sets exact input mode flags without changing the rest
	/// </summary>
	/// <param name="flags">Console input mode flags to set</param>
	public static void SetConsoleInputModeFlags(ConsoleInputMode flags) {
		if(!InputAvailable)
			return;
		SetConsoleInputMode(ConsoleInputMode | flags);
	}
	/// <summary>
	/// Clears exact output mode flags without changing the rest
	/// </summary>
	/// <param name="flags">Console output mode flags to set</param>
	public static void ClearConsoleOutputModeFlags(ConsoleOutputMode flags) {
		if(!OutputAvailable)
			return;
		SetConsoleOutputMode(ConsoleOutputMode & ~flags);
	}
	/// <summary>
	/// Clears exact input mode flags without changing the rest
	/// </summary>
	/// <param name="flags">Console input mode flags to set</param>
	public static void ClearConsoleInputModeFlags(ConsoleInputMode flags) {
		if(!InputAvailable)
			return;
		SetConsoleInputMode(ConsoleInputMode & ~flags);
	}
	/// <summary>
	/// Toggles exact output mode flags without changing the rest
	/// </summary>
	/// <param name="flags">Console output mode flags to set</param>
	public static void ToggleConsoleOutputModeFlags(ConsoleOutputMode flags) {
		if(!OutputAvailable)
			return;
		SetConsoleOutputMode(ConsoleOutputMode ^ flags);
	}
	/// <summary>
	/// Toggles exact input mode flags without changing the rest
	/// </summary>
	/// <param name="flags">Console input mode flags to set</param>
	public static void ToggleConsoleInputModeFlags(ConsoleInputMode flags) {
		if(!InputAvailable)
			return;
		SetConsoleInputMode(ConsoleInputMode ^ flags);
	}

	/// <summary>
	/// Unmanaged Win32 API for console functions
	/// </summary>
	public static class NativeApi {
		#region Constants
		/// <summary>
		/// Handle value returned if something went wrong
		/// </summary>
		public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
		/// <summary>
		/// Zero pointer
		/// </summary>
		public static readonly IntPtr NULL = IntPtr.Zero;
		/// <summary>
		/// The standard input device. Initially, this is the console input buffer, CONIN$.
		/// </summary>
		public const uint STD_INPUT_HANDLE = unchecked((uint) -10);
		/// <summary>
		/// The standard output device. Initially, this is the active console screen buffer, CONOUT$.
		/// </summary>
		public const uint STD_OUTPUT_HANDLE = unchecked((uint) -11);
		/// <summary>
		/// The standard error device. Initially, this is the active console screen buffer, CONOUT$.
		/// </summary>
		public const uint STD_ERROR_HANDLE = unchecked((uint) -12);
		/// <summary>
		/// <see cref="CONSOLE_SELECTION_INFO.dwFlags"/>. No selection
		/// </summary>
		public const uint CONSOLE_NO_SELECTION = 0x0000;
		/// <summary>
		/// <see cref="CONSOLE_SELECTION_INFO.dwFlags"/>. Selection has begun
		/// </summary>
		public const uint CONSOLE_SELECTION_IN_PROGRESS = 0x0001;
		/// <summary>
		/// <see cref="CONSOLE_SELECTION_INFO.dwFlags"/>. Selection rectangle is not empty
		/// </summary>
		public const uint CONSOLE_SELECTION_NOT_EMPTY = 0x0002;
		/// <summary>
		/// <see cref="CONSOLE_SELECTION_INFO.dwFlags"/>. Selecting with the mouse
		/// </summary>
		public const uint CONSOLE_MOUSE_SELECTION = 0x0004;
		/// <summary>
		/// <see cref="CONSOLE_SELECTION_INFO.dwFlags"/>. Mouse is down
		/// </summary>
		public const uint CONSOLE_MOUSE_DOWN = 0x0008;
		/// <summary>
		/// <see cref="INPUT_RECORD.EventType"/>. The <see cref="INPUT_RECORD.FocusEvent"/>
		/// member contains a valid <see cref="FOCUS_EVENT_RECORD"/> structure. These events
		/// are used internally and should be ignored.
		/// </summary>
		public const ushort FOCUS_EVENT = 0x0010;
		/// <summary>
		/// <see cref="INPUT_RECORD.EventType"/>. The <see cref="INPUT_RECORD.KeyEvent"/>
		/// member contains a valid <see cref="KEY_EVENT_RECORD"/> structure.
		/// </summary>
		public const ushort KEY_EVENT = 0x0001;
		/// <summary>
		/// <see cref="INPUT_RECORD.EventType"/>. The <see cref="INPUT_RECORD.MenuEvent"/>
		/// member contains a valid <see cref="MENU_EVENT_RECORD"/> structure. These events
		/// are used internally and should be ignored.
		/// </summary>
		public const ushort MENU_EVENT = 0x0008;
		/// <summary>
		/// <see cref="INPUT_RECORD.EventType"/>. The <see cref="INPUT_RECORD.MouseEvent"/>
		/// member contains a valid <see cref="MOUSE_EVENT_RECORD"/> structure.
		/// </summary>
		public const ushort MOUSE_EVENT = 0x0002;
		/// <summary>
		/// <see cref="INPUT_RECORD.EventType"/>. The <see cref="INPUT_RECORD.WindowBufferSizeEvent"/>
		/// member contains a valid <see cref="WINDOW_BUFFER_SIZE_RECORD"/> structure.
		/// </summary>
		public const ushort WINDOW_BUFFER_SIZE_EVENT = 0x0004;
		/// <summary>
		/// <see cref="MOUSE_EVENT_RECORD.dwControlKeyState"/> and 
		/// <see cref="KEY_EVENT_RECORD.dwControlKeyState"/>. The CAPS LOCK light is on.
		/// </summary>
		public const uint CAPSLOCK_ON = 0x0080;
		/// <summary>
		/// <see cref="MOUSE_EVENT_RECORD.dwControlKeyState"/> and 
		/// <see cref="KEY_EVENT_RECORD.dwControlKeyState"/>. The key is enhanced.
		/// </summary>
		public const uint ENHANCED_KEY = 0x0100;
		/// <summary>
		/// <see cref="MOUSE_EVENT_RECORD.dwControlKeyState"/> and 
		/// <see cref="KEY_EVENT_RECORD.dwControlKeyState"/>. The left ALT key is pressed.
		/// </summary>
		public const uint LEFT_ALT_PRESSED = 0x0002;
		/// <summary>
		/// <see cref="MOUSE_EVENT_RECORD.dwControlKeyState"/> and 
		/// <see cref="KEY_EVENT_RECORD.dwControlKeyState"/>. The left CTRL key is pressed.
		/// </summary>
		public const uint LEFT_CTRL_PRESSED = 0x0008;
		/// <summary>
		/// <see cref="MOUSE_EVENT_RECORD.dwControlKeyState"/> and 
		/// <see cref="KEY_EVENT_RECORD.dwControlKeyState"/>. The NUM LOCK light is on.
		/// </summary>
		public const uint NUMLOCK_ON = 0x0020;
		/// <summary>
		/// <see cref="MOUSE_EVENT_RECORD.dwControlKeyState"/> and 
		/// <see cref="KEY_EVENT_RECORD.dwControlKeyState"/>. The right ALT key is pressed.
		/// </summary>
		public const uint RIGHT_ALT_PRESSED = 0x0001;
		/// <summary>
		/// <see cref="MOUSE_EVENT_RECORD.dwControlKeyState"/> and 
		/// <see cref="KEY_EVENT_RECORD.dwControlKeyState"/>. The right CTRL key is pressed.
		/// </summary>
		public const uint RIGHT_CTRL_PRESSED = 0x0004;
		/// <summary>
		/// <see cref="MOUSE_EVENT_RECORD.dwControlKeyState"/> and 
		/// <see cref="KEY_EVENT_RECORD.dwControlKeyState"/>. The SCROLL LOCK light is on.
		/// </summary>
		public const uint SCROLLLOCK_ON = 0x0040;
		/// <summary>
		/// <see cref="MOUSE_EVENT_RECORD.dwControlKeyState"/> and 
		/// <see cref="KEY_EVENT_RECORD.dwControlKeyState"/>. The SHIFT key is pressed.
		/// </summary>
		public const uint SHIFT_PRESSED = 0x0010;
		/// <summary>
		/// <see cref="MOUSE_EVENT_RECORD.dwButtonState"/>. The leftmost mouse button.
		/// </summary>
		public const uint FROM_LEFT_1ST_BUTTON_PRESSED = 0x0001;
		/// <summary>
		/// <see cref="MOUSE_EVENT_RECORD.dwButtonState"/>. The second button from the left.
		/// </summary>
		public const uint FROM_LEFT_2ND_BUTTON_PRESSED = 0x0004;
		/// <summary>
		/// <see cref="MOUSE_EVENT_RECORD.dwButtonState"/>. The third button from the left.
		/// </summary>
		public const uint FROM_LEFT_3RD_BUTTON_PRESSED = 0x0008;
		/// <summary>
		/// <see cref="MOUSE_EVENT_RECORD.dwButtonState"/>. The fourth button from the left.
		/// </summary>
		public const uint FROM_LEFT_4TH_BUTTON_PRESSED = 0x0010;
		/// <summary>
		/// <see cref="MOUSE_EVENT_RECORD.dwButtonState"/>. The rightmost mouse button.
		/// </summary>
		public const uint RIGHTMOST_BUTTON_PRESSED = 0x0002;
		/// <summary>
		/// <see cref="MOUSE_EVENT_RECORD.dwEventFlags"/>. The second click (button press) of a
		/// double-click occurred. The first click is returned as a regular button-press event.
		/// </summary>
		public const uint DOUBLE_CLICK = 0x0002;
		/// <summary>
		/// <see cref="MOUSE_EVENT_RECORD.dwEventFlags"/>. The horizontal mouse wheel was moved.
		/// If the high word of the <see cref="MOUSE_EVENT_RECORD.dwButtonState"/> member
		/// contains a positive value, the wheel was rotated to the right.Otherwise, the wheel
		/// was rotated to the left.
		/// </summary>
		public const uint MOUSE_HWHEELED = 0x0008;
		/// <summary>
		/// <see cref="MOUSE_EVENT_RECORD.dwEventFlags"/>. A change in mouse position occurred.
		/// </summary>
		public const uint MOUSE_MOVED = 0x0001;
		/// <summary>
		/// <see cref="MOUSE_EVENT_RECORD.dwEventFlags"/>.The vertical mouse wheel was moved. 
		/// If the high word of the <see cref="MOUSE_EVENT_RECORD.dwButtonState"/> member
		/// contains a positive value, the wheel was rotated forward, away from the user.
		/// Otherwise, the wheel was rotated backward, toward the user.
		/// </summary>
		public const uint MOUSE_WHEELED = 0x0004;
		/// <summary>
		/// Character attribute. Text color contains blue.
		/// </summary>
		public const ushort FOREGROUND_BLUE = 0x0001;
		/// <summary>
		/// Character attribute. Text color contains green.
		/// </summary>
		public const ushort FOREGROUND_GREEN = 0x0002;
		/// <summary>
		/// Character attribute. Text color contains red.
		/// </summary>
		public const ushort FOREGROUND_RED = 0x0003;
		/// <summary>
		/// Character attribute. Text color is intensified.
		/// </summary>
		public const ushort FOREGROUND_INTENSITY = 0x0008;
		/// <summary>
		/// Character attribute. Background color contains blue.
		/// </summary>
		public const ushort BACKGROUND_BLUE = 0x0010;
		/// <summary>
		/// Character attribute. Background color contains green.
		/// </summary>
		public const ushort BACKGROUND_GREEN = 0x0020;
		/// <summary>
		/// Character attribute. Background color contains red.
		/// </summary>
		public const ushort BACKGROUND_RED = 0x0040;
		/// <summary>
		/// Character attribute. Background color is intensified.
		/// </summary>
		public const ushort BACKGROUND_INTENSITY = 0x0080;
		/// <summary>
		/// Character attribute. Leading byte.
		/// </summary>
		public const ushort COMMON_LVB_LEADING_BYTE = 0x0100;
		/// <summary>
		/// Character attribute. Trailing byte.
		/// </summary>
		public const ushort COMMON_LVB_TRAILING_BYTE = 0x0200;
		/// <summary>
		/// Character attribute. Top horizontal.
		/// </summary>
		public const ushort COMMON_LVB_GRID_HORIZONTAL = 0x0400;
		/// <summary>
		/// Character attribute. Left vertical.
		/// </summary>
		public const ushort COMMON_LVB_GRID_LVERTICAL = 0x0800;
		/// <summary>
		/// Character attribute. Right vertical.
		/// </summary>
		public const ushort COMMON_LVB_GRID_RVERTICAL = 0x1000;
		/// <summary>
		/// Character attribute. Reverse foreground and background attribute.
		/// </summary>
		public const ushort COMMON_LVB_REVERSE_VIDEO = 0x4000;
		/// <summary>
		/// Character attribute. Underscore.
		/// </summary>
		public const ushort COMMON_LVB_UNDERSCORE = 0x8000;
		/// <summary>
		/// <see cref="GetSystemMetrics(uint)"/> parameter. The minimum width of a window, in pixels. 
		/// </summary>
		public const uint SM_CXMIN = 28;
		/// <summary>
		/// <see cref="GetSystemMetrics(uint)"/> parameter. The minimum height of a window, in pixels. 
		/// </summary>
		public const uint SM_CYMIN = 29;
		/// <summary>
		/// Console mode appliable to input handle. Characters read by the
		/// <see cref="ReadConsole"/> function are written to the active screen buffer as they
		/// are read. This mode can be used only if the <see cref="ENABLE_LINE_INPUT"/> mode is
		/// also enabled.
		/// </summary>
		public const uint ENABLE_ECHO_INPUT = 0x0004;
		/// <summary>
		/// Console mode appliable to input handle. Required for
		/// <see cref="ENABLE_QUICK_EDIT_MODE"/> and <see cref="ENABLE_INSERT_MODE"/>
		/// </summary>
		public const uint ENABLE_EXTENDED_FLAGS = 0x0080;
		/// <summary>
		/// Console mode appliable to input handle. When enabled, text entered in a console
		/// window will be inserted at the current cursor location and all text following that
		/// location will not be overwritten. When disabled, all following text will be
		/// overwritten.
		/// </summary>
		public const uint ENABLE_INSERT_MODE = 0x0020;
		/// <summary>
		/// Console mode appliable to input handle. The <see cref="ReadConsole"/> function
		/// returns only when a carriage return character is read. If this mode is disabled,
		/// the functions return when one or more characters are available.
		/// </summary>
		public const uint ENABLE_LINE_INPUT = 0x0002;
		/// <summary>
		/// Console mode appliable to input handle. If the mouse pointer is within the borders
		/// of the console window and the window has the keyboard focus, mouse events
		/// generated by mouse movement and button presses are placed in the input buffer.
		/// These events are discarded by <see cref="ReadConsole"/>, even when this mode
		/// is enabled.
		/// </summary>
		public const uint ENABLE_MOUSE_INPUT = 0x0010;
		/// <summary>
		/// Console mode appliable to input handle. CTRL+C is processed by the system and is
		/// not placed in the input buffer. If the input buffer is being read by
		/// <see cref="ReadConsole"/>, other control keys are processed by the system and are
		/// not returned in the <see cref="ReadConsole"/> buffer. If the
		/// <see cref="ENABLE_LINE_INPUT"/> mode is also enabled, backspace, carriage return,
		/// and line feed characters are handled by the system.
		/// </summary>
		public const uint ENABLE_PROCESSED_INPUT = 0x0001;
		/// <summary>
		/// Console mode appliable to input handle. This flag enables the user to use the
		/// mouse to select and edit text. Requires <see cref="ENABLE_EXTENDED_FLAGS"/>.
		/// </summary>
		public const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
		/// <summary>
		/// Console mode appliable to input handle. User interactions that change the size of
		/// the console screen buffer are reported in the console's input buffer.
		/// Information about these events can be read from the input buffer by applications
		/// using the <see cref="ReadConsoleInput"/> function, but not by those using
		/// <see cref="ReadConsole"/>.
		/// </summary>
		public const uint ENABLE_WINDOW_INPUT = 0x0008;
		/// <summary>
		/// Console mode appliable to input handle. Setting this flag directs the Virtual
		/// Terminal processing engine to convert user input received by the console window
		/// into Console Virtual Terminal Sequences that can be retrieved by a supporting
		/// application through <see cref="WriteConsole"/> functions. <para></para>
		/// The typical usage of
		/// this flag is intended in conjunction with
		/// <see cref="ENABLE_VIRTUAL_TERMINAL_PROCESSING"/> on the output handle to connect
		/// to an application that communicates exclusively via virtual terminal sequences.
		/// </summary>
		public const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200;
		/// <summary>
		/// Console mode appliable to output handle. Characters written by the
		/// <see cref="WriteConsole"/> function or echoed by the <see cref="ReadConsole"/>
		/// function are parsed for ASCII control sequences, and the correct action is
		/// performed. Backspace, tab, bell, carriage return, and line feed characters are
		/// processed.
		/// </summary>
		public const uint ENABLE_PROCESSED_OUTPUT = 0x0001;
		/// <summary>
		/// Console mode appliable to output handle. When writing with <see cref="WriteConsole"/>
		/// or echoing with <see cref="ReadConsole"/>, the cursor moves to the beginning of
		/// the next row when it reaches the end of the current row. This causes the
		/// rows displayed in the console window to scroll up automatically when the
		/// cursor advances beyond the last row in the window. It also causes the contents
		/// of the console screen buffer to scroll up (discarding the top row of the console
		/// screen buffer) when the cursor advances beyond the last row in the console
		/// screen buffer. If this mode is disabled, the last character in the row is
		/// overwritten with any subsequent characters.
		/// </summary>
		public const uint ENABLE_WRAP_AT_EOL_OUTPUT = 0x0002;
		/// <summary>
		/// Console mode appliable to output handle. When writing with <see cref="WriteConsole"/>,
		/// characters are parsed for VT100 and similar control character sequences that control
		/// cursor movement, color/font mode, and other operations that can also be performed
		/// via the existing Console APIs. For more information, see Console Virtual Terminal
		/// Sequences.
		/// </summary>
		public const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
		/// <summary>
		/// Console mode appliable to output handle. When writing with <see cref="WriteConsole"/>,
		/// this adds an additional state to end-of-line wrapping that can delay the cursor
		/// move and buffer scroll operations. <para></para>
		/// Normally when <see cref="ENABLE_WRAP_AT_EOL_OUTPUT"/> is set and text reaches
		/// the end of the line, the cursor will immediately move to the next line and the
		/// contents of the buffer will scroll up by one line.In contrast with this flag set,
		/// the scroll operation and cursor move is delayed until the next character arrives.
		/// The written character will be printed in the final position on the line and the
		/// cursor will remain above this character as if <see cref="ENABLE_WRAP_AT_EOL_OUTPUT"/>
		/// was off, but the next printable character will be printed as if
		/// <see cref="ENABLE_WRAP_AT_EOL_OUTPUT"/> is on. No overwrite will occur. Specifically,
		/// the cursor quickly advances down to the following line, a scroll is performed if
		/// necessary, the character is printed, and the cursor advances one more position. <para></para>
		/// The typical usage of this flag is intended in conjunction with setting
		/// <see cref="ENABLE_VIRTUAL_TERMINAL_PROCESSING"/> to better emulate a terminal
		/// emulator where writing the final character on the screen (in the bottom right corner)
		/// without triggering an immediate scroll is the desired behavior.
		/// </summary>
		public const uint ENABLE_NEWLINE_AUTO_RETURN = 0x0008;
		/// <summary>
		/// Console mode appliable to output handle. The APIs for writing character attributes
		/// including <see cref="WriteConsoleOutput"/> and
		/// <see cref="WriteConsoleOutputAttribute"/> allow the usage of flags from character
		/// attributes to adjust the color of the foreground and background of text.
		/// Additionally, a range of DBCS flags was specified with the COMMON_LVB prefix.
		/// Historically, these flags only functioned in DBCS code pages for Chinese, Japanese,
		/// and Korean languages. With exception of the leading byte and trailing byte flags,
		/// the remaining flags describing line drawing and reverse video (swap foreground and
		/// background colors) can be useful for other languages to emphasize portions of output.
		/// <para></para> With exception of the leading byte and trailing byte flags,
		/// the remaining flags describing line drawing and reverse video(swap foreground and
		/// background colors) can be useful for other languages to emphasize portions of output.
		/// <para></para> Setting this console mode flag will allow these attributes to be used
		/// in every code page on every language.
		/// <para></para> It is off by default to maintain compatibility with known applications
		/// that have historically taken advantage of the console ignoring these flags on
		/// non-CJK machines to store bits in these fields for their own purposes or by accident.
		/// <para></para> Note that using the <see cref="ENABLE_VIRTUAL_TERMINAL_PROCESSING"/>
		/// mode can result in LVB grid and reverse video flags being set while this flag is
		/// still off if the attached application requests underlining or inverse video via
		/// Console Virtual Terminal Sequences.
		/// </summary>
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
			[In] ref CONSOLE_READCONSOLE_CONTROL pInputControl);
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
			/// <see cref="CAPSLOCK_ON"/>, <see cref="ENHANCED_KEY"/>,
			/// <see cref="LEFT_ALT_PRESSED"/>, <see cref="LEFT_CTRL_PRESSED"/>,
			/// <see cref="NUMLOCK_ON"/>, <see cref="RIGHT_ALT_PRESSED"/>,
			/// <see cref="RIGHT_CTRL_PRESSED"/>, <see cref="SCROLLLOCK_ON"/>,
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
/// <summary>
/// Possible values for <see cref="Win32Console.SetConsoleOutputMode"/> parameter. Can be combined.
/// </summary>
[Flags]
public enum ConsoleOutputMode {
	None = 0,
	/// <summary>
	/// Characters written by the <see cref="Win32Console.Write(object)"/> function or echoed by
	/// the <see cref="Win32Console.Read"/> function are parsed for ASCII control sequences,
	/// and the correct action is performed. Backspace, tab, bell, carriage return, and line
	/// feed characters are processed.
	/// </summary>
	ProcessedOutput = (int) ENABLE_PROCESSED_OUTPUT,
	/// <summary>
	/// When writing with <see cref="Win32Console.Write(object)"/> or echoing with
	/// <see cref="Win32Console.Read"/>, the cursor moves to the beginning of
	/// the next row when it reaches the end of the current row. This causes the
	/// rows displayed in the console window to scroll up automatically when the
	/// cursor advances beyond the last row in the window. It also causes the contents
	/// of the console screen buffer to scroll up (discarding the top row of the console
	/// screen buffer) when the cursor advances beyond the last row in the console
	/// screen buffer. If this mode is disabled, the last character in the row is
	/// overwritten with any subsequent characters.
	/// </summary>
	WrapAtEolOutput = (int) ENABLE_WRAP_AT_EOL_OUTPUT,
	/// <summary>
	/// When writing with <see cref="Win32Console.Write(object)"/>, characters are parsed for
	/// VT100 and similar control character sequences that control cursor movement,
	/// color/font mode, and other operations that can also be performed via the existing
	/// Console APIs. For more information, see Console Virtual Terminal Sequences.
	/// </summary>
	VirtualTerminalProcessing = (int) ENABLE_VIRTUAL_TERMINAL_PROCESSING,
	/// <summary>
	/// Console mode appliable to output handle. When writing with
	/// <see cref="Win32Console.Write(object)"/>, this adds an additional state to
	/// end-of-line wrapping that can delay the cursor move and buffer scroll operations.
	/// <para></para> Normally when <see cref="WrapAtEolOutput"/> is set and text reaches
	/// the end of the line, the cursor will immediately move to the next line and the
	/// contents of the buffer will scroll up by one line.In contrast with this flag set,
	/// the scroll operation and cursor move is delayed until the next character arrives.
	/// The written character will be printed in the final position on the line and the
	/// cursor will remain above this character as if <see cref="WrapAtEolOutput"/>
	/// was off, but the next printable character will be printed as if
	/// <see cref="WrapAtEolOutput"/> is on. No overwrite will occur. Specifically,
	/// the cursor quickly advances down to the following line, a scroll is performed if
	/// necessary, the character is printed, and the cursor advances one more position.
	/// <para></para> The typical usage of this flag is intended in conjunction with setting
	/// <see cref="VirtualTerminalProcessing"/> to better emulate a terminal
	/// emulator where writing the final character on the screen (in the bottom right corner)
	/// without triggering an immediate scroll is the desired behavior.
	/// </summary>
	NewlineAutoReturn = (int) ENABLE_NEWLINE_AUTO_RETURN,
	/// <summary>
	/// Console mode appliable to output handle. The APIs for writing character attributes
	/// allow the usage of flags from character attributes to adjust the color of the foreground
	/// and background of text. Additionally, a range of DBCS flags was specified with the
	/// COMMON_LVB prefix. Historically, these flags only functioned in DBCS code pages for
	/// Chinese, Japanese, and Korean languages. With exception of the leading byte and trailing
	/// byte flags, the remaining flags describing line drawing and reverse video
	/// (swap foreground and background colors) can be useful for other languages to emphasize
	/// portions of output.
	/// <para></para> With exception of the leading byte and trailing byte flags,
	/// the remaining flags describing line drawing and reverse video(swap foreground and
	/// background colors) can be useful for other languages to emphasize portions of output.
	/// <para></para> Setting this console mode flag will allow these attributes to be used
	/// in every code page on every language.
	/// <para></para> It is off by default to maintain compatibility with known applications
	/// that have historically taken advantage of the console ignoring these flags on
	/// non-CJK machines to store bits in these fields for their own purposes or by accident.
	/// <para></para> Note that using the <see cref="VirtualTerminalProcessing"/>
	/// mode can result in LVB grid and reverse video flags being set while this flag is
	/// still off if the attached application requests underlining or inverse video via
	/// Console Virtual Terminal Sequences.
	/// </summary>
	LvbGridWorldwide = (int) ENABLE_LVB_GRID_WORLDWIDE
}
/// <summary>
/// Possible values for <see cref="Win32Console.SetConsoleInputMode"/> parameter. Can be combined.
/// </summary>
[Flags]
public enum ConsoleInputMode {
	None = 0,
	/// <summary>
	/// Console mode appliable to input handle. Required for <see cref="QuickEditMode"/> and
	/// <see cref="InsertMode"/>
	/// </summary>
	ExtendedFlags = (int) ENABLE_EXTENDED_FLAGS,
	/// <summary>
	/// Console mode appliable to input handle. Characters read by the
	/// <see cref="Win32Console.Read"/> function are written to the active screen buffer as they
	/// are read. This mode can be used only if the <see cref="LineInput"/> mode is also enabled.
	/// </summary>
	EchoInput = (int) ENABLE_ECHO_INPUT,
	/// <summary>
	/// Console mode appliable to input handle. When enabled, text entered in a console
	/// window will be inserted at the current cursor location and all text following that
	/// location will not be overwritten. When disabled, all following text will be
	/// overwritten.
	/// </summary>
	InsertMode = (int) ENABLE_INSERT_MODE,
	/// <summary>
	/// Console mode appliable to input handle. The <see cref="Win32Console.Read"/> function
	/// returns only when a carriage return character is read. If this mode is disabled,
	/// the functions return when one or more characters are available.
	/// </summary>
	LineInput = (int) ENABLE_LINE_INPUT,
	/// <summary>
	/// Console mode appliable to input handle. If the mouse pointer is within the borders
	/// of the console window and the window has the keyboard focus, mouse events
	/// generated by mouse movement and button presses are placed in the input buffer.
	/// These events are discarded by <see cref="Win32Console.Read"/>, even when this mode
	/// is enabled.
	/// </summary>
	MouseInput = (int) ENABLE_MOUSE_INPUT,
	/// <summary>
	/// Console mode appliable to input handle. CTRL+C is processed by the system and is
	/// not placed in the input buffer. If the input buffer is being read by
	/// <see cref="Win32Console.Read"/>, other control keys are processed by the system and are
	/// not returned in the <see cref="Win32Console.Read"/> buffer. If the
	/// <see cref="LineInput"/> mode is also enabled, backspace, carriage return,
	/// and line feed characters are handled by the system.
	/// </summary>
	ProcessedInput = (int) ENABLE_PROCESSED_INPUT,
	/// <summary>
	/// Console mode appliable to input handle. This flag enables the user to use the
	/// mouse to select and edit text. Requires <see cref="ExtendedFlags"/>.
	/// </summary>
	QuickEditMode = (int) ENABLE_QUICK_EDIT_MODE,
	/// <summary>
	/// Console mode appliable to input handle. User interactions that change the size of
	/// the console screen buffer are reported in the console's input buffer.
	/// Information about these events can be read from the input buffer by applications
	/// using the <see cref="Win32Console.NextInputEvent"/> function, but not by those using
	/// <see cref="Win32Console.Read"/>.
	/// </summary>
	WindowInput = (int) ENABLE_WINDOW_INPUT,
	/// <summary>
	/// Console mode appliable to input handle. Setting this flag directs the Virtual
	/// Terminal processing engine to convert user input received by the console window
	/// into Console Virtual Terminal Sequences that can be retrieved by a supporting
	/// application through <see cref="Win32Console.Write(object)"/> functions.
	/// <para></para> The typical usage of this flag is intended in conjunction with
	/// <see cref="ConsoleOutputMode.VirtualTerminalProcessing"/> on the output handle to connect
	/// to an application that communicates exclusively via virtual terminal sequences.
	/// </summary>
	VirtualTerminalInput = (int) ENABLE_VIRTUAL_TERMINAL_INPUT
}
/// <summary>
/// General interface for <see cref="KeyInputEvent"/>, <see cref="MouseInputEvent"/> and
/// <see cref="WindowBufferSizeInputEvent"/>
/// </summary>
public interface IInputEvent { }
/// <summary>
/// Control key state values for <see cref="KeyInputEvent.ControlKeyState"/> and
/// <see cref="MouseInputEvent.ControlKeyState"/>. Can be combined.
/// </summary>
[Flags]
public enum ControlKeyState {
	None = 0,
	/// <summary>
	/// The CAPS LOCK light is on.
	/// </summary>
	CapsLock = (int) CAPSLOCK_ON,
	/// <summary>
	/// The key is enhanced.
	/// </summary>
	Enhanced = (int) ENHANCED_KEY,
	/// <summary>
	/// The left ALT key is pressed.
	/// </summary>
	LeftAlt = (int) LEFT_ALT_PRESSED,
	/// <summary>
	/// The left CTRL key is pressed.
	/// </summary>
	LeftCtrl = (int) LEFT_CTRL_PRESSED,
	/// <summary>
	/// The NUM LOCK light is on.
	/// </summary>
	NumLock = (int) NUMLOCK_ON,
	/// <summary>
	/// The right ALT key is pressed.
	/// </summary>
	RightAlt = (int) RIGHT_ALT_PRESSED,
	/// <summary>
	/// The right CTRL key is pressed.
	/// </summary>
	RightCtrl = (int) RIGHT_CTRL_PRESSED,
	/// <summary>
	/// The SCROLL LOCK light is on.
	/// </summary>
	ScrollLock = (int) SCROLLLOCK_ON,
	/// <summary>
	/// The SHIFT key is pressed.
	/// </summary>
	Shift = (int) SHIFT_PRESSED
}
/// <summary>
/// Values for <see cref="MouseInputEvent.Type"/>.
/// </summary>
[Flags]
public enum MouseInputEventType {
	Click = 0,
	/// <summary>
	/// The second click (button press) of a double-click occurred. The first click is returned
	/// as a regular button-press event <see cref="Click"/>.
	/// </summary>
	DoubleClick = (int) DOUBLE_CLICK,
	/// <summary>
	/// The horizontal mouse wheel was moved.
	/// </summary>
	HorizontalWheel = (int) MOUSE_HWHEELED,
	/// <summary>
	/// A change in mouse position occurred.
	/// </summary>
	Moved = (int) MOUSE_MOVED,
	/// <summary>
	/// The vertical mouse wheel was moved.
	/// </summary>
	VerticalWheel = (int) MOUSE_WHEELED
}
/// <summary>
/// Key input event received from <see cref="Win32Console.NextInputEvent"/>
/// </summary>
public class KeyInputEvent : IInputEvent {
	/// <summary>
	/// Whether key has been pressed or released
	/// </summary>
	public bool IsKeyDown { get; }
	/// <summary>
	/// Amount of times key has been pressed
	/// </summary>
	public int RepeatCount { get; }
	/// <summary>
	/// Virtual key code of key pressed
	/// </summary>
	public uint KeyCode { get; }
	/// <summary>
	/// Virtual scan code received from hardware
	/// </summary>
	public uint ScanCode { get; }
	/// <summary>
	/// Unicode character assigned to key
	/// </summary>
	public char Char { get; }
	/// <summary>
	/// Control key's states info
	/// </summary>
	public ControlKeyState ControlKeyState { get; }

	/// <summary>
	/// Wraps unmanaged event structure
	/// </summary>
	/// <param name="native">Unmanaged event structure</param>
	public KeyInputEvent(KEY_EVENT_RECORD native) {
		IsKeyDown = native.bKeyDown;
		RepeatCount = native.wRepeatCount;
		KeyCode = native.wVirtualKeyCode;
		ScanCode = native.wVirtualScanCode;
		Char = native.UnicodeChar;
		ControlKeyState = (ControlKeyState) native.dwControlKeyState;
	}
}
/// <summary>
/// Mouse input event received from <see cref="Win32Console.NextInputEvent"/>
/// </summary>
public class MouseInputEvent : IInputEvent {
	/// <summary>
	/// Mouse horizontal position when event occured in terms of the screen buffer's
	/// character-cell coordinates.
	/// </summary>
	public int MouseX { get; }
	/// <summary>
	/// Mouse vertical position when event occured in terms of the screen buffer's
	/// character-cell coordinates.
	/// </summary>
	public int MouseY { get; }
	/// <summary>
	/// True, if left mouse button is down
	/// </summary>
	public bool LeftButton { get; }
	/// <summary>
	/// True, if middle mouse button is down
	/// </summary>
	public bool MiddleButton { get; }
	/// <summary>
	/// True, if right mouse button is down
	/// </summary>
	public bool RightButton { get; }
	/// <summary>
	/// True, if first mouse XButton is down
	/// </summary>
	public bool XButton1 { get; }
	/// <summary>
	/// True, if second mouse XButton is down
	/// </summary>
	public bool XButton2 { get; }
	/// <summary>
	/// Wheel change amount. Positive, if wheeled right or forward
	/// </summary>
	public int Wheel { get; }
	/// <summary>
	/// Control key's states info
	/// </summary>
	public ControlKeyState ControlKeyState { get; }
	/// <summary>
	/// The type of event
	/// </summary>
	public MouseInputEventType Type { get; }

	/// <summary>
	/// Wraps unmanaged event structure
	/// </summary>
	/// <param name="native">Unmanaged event structure</param>
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
		Wheel = unchecked((int)(native.dwButtonState & 0xFFFF0000U));
	}
}
/// <summary>
/// Window buffer size input event received from <see cref="Win32Console.NextInputEvent"/>
/// </summary>
public class WindowBufferSizeInputEvent : IInputEvent {
	/// <summary>
	/// Console screen buffer width in character-cell terms
	/// </summary>
	public int Width { get; }
	/// <summary>
	/// Console screen buffer height in character-cell terms
	/// </summary>
	public int Height { get; }

	/// <summary>
	/// Wraps unmanaged event structure
	/// </summary>
	/// <param name="native">Unmanaged event structure</param>
	public WindowBufferSizeInputEvent(WINDOW_BUFFER_SIZE_RECORD native) {
		Width = native.dwSize.X;
		Height = native.dwSize.Y;
	}
}
