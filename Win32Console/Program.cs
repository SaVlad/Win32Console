using System;

class Program {
	private static V Decode<K, V>(K key, params (K, V)[] dict) {
		foreach((K key, V val) kvp in dict)
			if(kvp.key.Equals(key))
				return kvp.val;
		return default;
	}
	public static void Main(string[] args) {
		Win32Console.SetConsoleInputModeFlags(
			ConsoleInputMode.WindowInput |
			ConsoleInputMode.EchoInput |
			ConsoleInputMode.MouseInput |
			ConsoleInputMode.ExtendedFlags);
		Win32Console.ClearConsoleInputModeFlags(
			ConsoleInputMode.ProcessedInput |
			ConsoleInputMode.QuickEditMode);
		for(short i = 0; i < 10; ++i)
			Win32Console.SetColor(i, i, ConsoleColor.Black, ConsoleColor.Gray);
		Win32Console.PutString(3, 5, "Test");
		Win32Console.WriteLine();
		Win32Console.WriteInput("test");
		Win32Console.Read();
	}
}