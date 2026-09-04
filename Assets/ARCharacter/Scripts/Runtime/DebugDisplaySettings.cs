namespace ARCharacter
{
    /// <summary>
    /// Единый выключатель для всей отладочной оснастки на экране (цифры перцепции,
    /// кнопки размещения). Перед записью чистого демо ставьте false и пересобирайте
    /// APK — выключать каждый оверлей поштучно легко забыть один из них.
    /// </summary>
    public static class DebugDisplaySettings
    {
        public static bool ShowDebugUI = true;
    }
}
