using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using JsonApiDotNetCore.Extensions;
using JsonApiDotNetCore.Internal.Contracts;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Query;
using JsonApiDotNetCore.Serialization.Server.Builders;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCore.Serialization.Server {
    public class ResponseResourceObjectBuilder : ResourceObjectBuilder {
        private readonly IIncludedResourceObjectBuilder _includedBuilder;
        private readonly IIncludeService _includeService;
        private readonly ILinkBuilder _linkBuilder;
        private readonly IFieldsToSerialize _fieldsToSerialize;
        private RelationshipAttribute _requestRelationship;
        private readonly ILogger<ResponseResourceObjectBuilder> _logger;

        public ResponseResourceObjectBuilder(IFieldsToSerialize fieldsToSerialize,
                                             ILinkBuilder linkBuilder,
                                             IIncludedResourceObjectBuilder includedBuilder,
                                             IIncludeService includeService,
                                             IResourceContextProvider provider,
                                             IResourceObjectBuilderSettingsProvider settingsProvider,
                                             ILoggerFactory loggerFactory)
            : base(provider, settingsProvider.Get()) {
            _logger = loggerFactory.CreateLogger<ResponseResourceObjectBuilder>();
            _linkBuilder = linkBuilder;
            _includedBuilder = includedBuilder;
            _includeService = includeService;
            _fieldsToSerialize = fieldsToSerialize;
        }

        public RelationshipEntry Build(IIdentifiable entity, RelationshipAttribute requestRelationship) {
            _requestRelationship = requestRelationship;
            return GetRelationshipData(requestRelationship, entity);
        }

        /// <summary>
        /// Builds the values of the relationships object on a resource object.
        /// The server serializer only populates the "data" member when the relationship is included,
        /// and adds links unless these are turned off. This means that if a relationship is not included
        /// and links are turned off, the entry would be completely empty, ie { }, which is not conform
        /// json:api spec. In that case we return null which will omit the entry from the output.
        /// </summary>
        protected override RelationshipEntry GetRelationshipData(RelationshipAttribute relationship, IIdentifiable entity) {
            RelationshipEntry relationshipEntry = null;
            List<List<RelationshipAttribute>> relationshipChains = null;

            relationshipEntry = base.GetRelationshipData(relationship, entity);

            // sven
            if (Equals(relationship, _requestRelationship) || ShouldInclude(relationship, out relationshipChains) || true) {
                if (relationshipChains != null && relationshipChains.Count != 0 && relationshipEntry.HasResource) {

                    // sven
                    //if (relationshipChains.Count == 0) {
                    //    relationshipChains = _fieldsToSerialize.GetAllowedRelationships(relationship.RightType)
                    //        .Select(x => new List<RelationshipAttribute> { x }).ToList();
                    //}

                    // every chain in relationshipChains has the same on first position = the relationship requested
                    var allowedRelations = _fieldsToSerialize.GetAllowedRelationships(relationship.RightType);
                    //var fakeChains = relationshipChains.ToList();
                    var fakeChains = new List<List<RelationshipAttribute>>();

                    var singleOneIndex = fakeChains.FindIndex(x => x.Count == 1 && x.First().Equals(relationship));
                    if (singleOneIndex != -1) {
                        fakeChains.RemoveAt(singleOneIndex);
                    }

                    foreach (var relation in allowedRelations) {
                        bool isIncluded = fakeChains.Any(x => x.Last().Equals(relation));
                        if (! isIncluded) {
                            var subList = new List<RelationshipAttribute> { relationship, relation };
                            fakeChains.Add(subList);
                        }
                    }

                    // sven
                    foreach (var chain in fakeChains) {
                        // traverses (recursively) and extracts all (nested) related entities for the current inclusion chain.
                        _includedBuilder.IncludeRelationshipChain(chain, entity);
                    }
                }
            }

            var links = _linkBuilder.GetRelationshipLinks(relationship, entity);
            // sven
            // omit links everytime
            //if (links != null)
            //    // if links relationshipLinks should be built for this entry, populate the "links" field.
            //    (relationshipEntry ??= new RelationshipEntry()).Links = links;

            // if neither "links" nor "data" was populated, return null, which will omit this entry from the output.
            // (see the NullValueHandling settings on <see cref="ResourceObject"/>)
            return relationshipEntry;
        }

        /// <summary>
        /// Inspects the included relationship chains (see <see cref="IIncludeService"/>
        /// to see if <paramref name="relationship"/> should be included or not.
        /// </summary>
        private bool ShouldInclude(RelationshipAttribute relationship, out List<List<RelationshipAttribute>> inclusionChain) {
            inclusionChain = _includeService.Get()?.Where(l => l.First().Equals(relationship)).ToList();
            return inclusionChain != null && inclusionChain.Any();
        }
    }

}
