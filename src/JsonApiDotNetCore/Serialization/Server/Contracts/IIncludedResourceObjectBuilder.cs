using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace JsonApiDotNetCore.Serialization.Server.Builders
{
    public interface IIncludedResourceObjectBuilder
    {
        /// <summary>
        /// Gets the list of resource objects representing the included entities
        /// </summary>
        List<ResourceObject> Build();

                    
        /// <summary>
        /// Extracts the included entities from <paramref name="rootEntity"/> using the
        /// (arbitrarily deeply nested) included relationships in <paramref name="inclusionChain"/>.
        /// </summary>
        void IncludeRelationshipChain(List<RelationshipAttribute> inclusionChain, IIdentifiable rootEntity);
    }
}
