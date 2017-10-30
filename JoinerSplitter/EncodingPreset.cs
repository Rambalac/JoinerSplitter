namespace JoinerSplitter
{
    using System;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Runtime.Serialization;
    using JetBrains.Annotations;

    [DataContract]
    [Serializable]
    public class EncodingPreset
    {
        private string displayName;

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string Value { get; set; }

        public string DisplayName
        {
            get => displayName ?? Name;
            set => displayName = value;
        }
    }
}