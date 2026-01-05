namespace MediaViewer.Extensions
{
    /// <summary>
    /// Represents File Explorer sort configuration
    /// </summary>
    public struct SortEntry
    {
        public string PropertyName { get; set; }
        public bool AscendingOrder { get; set; }

        public SortEntry(string propertyName, bool ascendingOrder)
        {
            PropertyName = propertyName;
            AscendingOrder = ascendingOrder;
        }
    }
}
