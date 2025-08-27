namespace DLS.Description
{
	public enum ChipType
	{
		Custom,

		// ---- Basic Chips ----
		Nand,
		TriStateBuffer,
		Clock,
		Pulse,
		Detector,

		// ---- Memory ----
		dev_Ram_8Bit,
		Rom_256x16,
		EEPROM_256x16,

		// ---- Displays ----
		SevenSegmentDisplay,
		DisplayRGB,
		DisplayDot,
		DisplayLED,
		DisplayRGBTouch,

		// ---- Merge / Split ----
		Merge_Pin,
		Split_Pin,

		// ---- In / Out Pins ----
		In_Pin,
		Out_Pin,

        Key,

		Button,
		Toggle,

		Constant_8Bit,

        // ---- Buses ----
        Bus,
		BusTerminus,
		
		// ---- Audio ----
		Buzzer,

		// ---- Time ----
		RTC,

		// ---- Clock ----
		SPS,
	}
}