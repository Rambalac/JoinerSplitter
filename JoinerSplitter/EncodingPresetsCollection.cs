namespace JoinerSplitter
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [DataContract]
    [Serializable]
    public class EncodingPresetsCollection : List<EncodingPreset>
    {
        public EncodingPresetsCollection()
        {
        }

        public EncodingPresetsCollection(IEnumerable<EncodingPreset> encodingPresets)
            : base(encodingPresets)
        {
        }
    }
}