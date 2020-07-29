using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JsonApiDotNetCore.Models
{
    public abstract class ExposableData<T> where T : class
    {
        /// <summary>
        /// see "primary data" in https://jsonapi.org/format/#document-top-level.
        /// </summary>
        [JsonProperty("data")]
        public object Data
        {
            get => GetPrimaryData();
            set => SetPrimaryData(value);
        }

        /// <summary>
        /// see https://www.newtonsoft.com/json/help/html/ConditionalProperties.htm
        /// </summary>
        /// <remarks>
        /// Moving this method to the derived class where it is needed only in the
        /// case of <see cref="RelationshipEntry"/> would make more sense, but
        /// Newtonsoft does not support this.
        /// </remarks>
        public bool ShouldSerializeData()
        {
            if (GetType() == typeof(RelationshipEntry))
                return IsPopulated;
            return true;
        }

        /// <summary>
        /// Internally used for "single" primary data.
        /// </summary>
        [JsonIgnore]
        public T SingleData { get; private set; }

        /// <summary>
        /// Internally used for "many" primary data.
        /// </summary>
        [JsonIgnore]
        public List<T> ManyData { get; private set; }

        /// <summary>
        /// Used to indicate if the document's primary data is "single" or "many".
        /// </summary>
        [JsonIgnore]
        public bool IsManyData { get; private set; }

        /// <summary>
        /// Internally used to indicate if the document's primary data
        /// should still be serialized when it's value is null. This is used when
        /// a single resource is requested but not present (eg /articles/1/author).
        /// </summary>
        internal bool IsPopulated { get; private set; }

        internal bool HasResource => IsPopulated && ((IsManyData && ManyData.Any()) || SingleData != null);

        /// <summary>
        /// Gets the "single" or "many" data depending on which one was
        /// assigned in this document.
        /// </summary>
        protected object GetPrimaryData()
        {
            if (IsManyData)
                return ManyData;
            return SingleData;
        }

        /// <summary>
        /// Sets the primary data depending on if it is "single" or "many" data.
        /// </summary>
        protected void SetPrimaryData(object value)
        {
            IsPopulated = true;
            if (value is JObject jObject)
                SingleData = jObject.ToObject<T>();
            else if (value is T ro)
                SingleData = ro;
            else if (value != null)
            {
                IsManyData = true;
                if (value is JArray jArray)
                    ManyData = jArray.ToObject<List<T>>();
                else
                    ManyData = (List<T>)value;
            }
        }
    }
}
