namespace ARCharacter.EditorTools
{
    /// <summary>
    /// Общий масштаб персонажа для всех сборщиков ассетов. Вынесено отдельно, чтобы
    /// SandboxSceneBuilder и CharacterAnimatorBuilder не могли разойтись в цифрах.
    /// </summary>
    internal static class CharacterDimensions
    {
        // Миниатюра: рост Mixamo-гуманоида около 1.8 м, здесь — 40 см.
        public const float Height = 0.4f;
    }
}
