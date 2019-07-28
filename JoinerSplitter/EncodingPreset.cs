namespace JoinerSplitter
{
    using System;
    using System.Runtime.Serialization;

    [DataContract]
    [Serializable]
    public class EncodingPreset
    {
        private string displayName;

        [DataMember]
        public string Name { get; set; }

        [DataMember(Name = "Value")]
        public string OutputEncoding { get; set; }

        [DataMember]
        public string ComplexFilter { get; set; }

        public string DisplayName
        {
            get => displayName ?? Name;
            set => displayName = value;
        }
    }
}