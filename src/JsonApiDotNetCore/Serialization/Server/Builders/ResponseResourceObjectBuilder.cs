using System;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Internal.Contracts;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Query;
using JsonApiDotNetCore.Serialization.Server.Builders;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCore.Serialization.Server {
    public class ResponseResourceObjectBuilder : ResourceObjectBuilder {
        private readonly IIncludedResourceObjectBuilder _includedBuilder;
        private readonly IIncludeService _includeService;
        private readonly ILinkBuilder _linkBuilder;
        private RelationshipAttribute _requestRelationship;
        private readonly ILogger<ResponseResourceObjectBuilder> _logger;

        public ResponseResourceObjectBuilder(ILinkBuilder linkBuilder,
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

            var type = entity.GetType();

            //var s = _requestRelationship.PublicRelationshipName;
            // sven
            _logger.LogCritical("-- GetRelationshipData()");
            _logger.LogWarning("-- relationship: {0}, requestRelationship: {1}", relationship, _requestRelationship);
            _logger.LogWarning("-- entity {0}", entity);


            // lets try adding it manually
            //_requestRelationship = relationship;
            _logger.LogWarning("-- includeService.List {0}", _includeService.Get().Count);
            _includeService.Get().Add(new List<RelationshipAttribute> { relationship });
            _logger.LogWarning("-- includeService.List {0}", _includeService.Get().Count);



            var shouldInclude = ShouldInclude(relationship, out relationshipChains);
            _logger.LogWarning("-- shouldInclude {0}", shouldInclude);
            _logger.LogWarning("-- relationshipChains.Count {0}", relationshipChains.Count);

            if (relationshipChains.Count == 0) {
                relationshipChains = _includeService.Get().ToList();
                _logger.LogWarning("-- relationshipChains.Count {0}", relationshipChains.Count);
            }
            // always count zero
            foreach (var item in relationshipChains) {
                _logger.LogWarning("-- item in chain {0}", item);
            }
            relationshipEntry = base.GetRelationshipData(relationship, entity);
            //relationshipEntry.Data = new ResourceIdentifierObject { Id = "sven_id", Type = "sven_type" };

            var entityType = entity.GetType();
            var propInfo = relationship.PropertyInfo;
            _logger.LogInformation("-- propInfo {0}", propInfo);
            _logger.LogInformation("-- propInfoName {0}", propInfo.Name);
            _logger.LogInformation("-- propInfoType {0}", propInfo.PropertyType);
            _logger.LogInformation("-- propInfoTypeName {0}", propInfo.PropertyType.Name);
            _logger.LogInformation("-- propInfoAttributes {0}", propInfo.Attributes);
            _logger.LogInformation("-- propInfoEntity {0}", propInfo.GetValue(entity));

            //_logger.LogInformation("-- propInfoId {0}", propInfo.GetValue("id"));
            //_logger.LogInformation("-- propInfoRelationship {0}", propInfo.GetValue(relationship));
            //_logger.LogInformation("-- propPublicName {0}", propInfo.GetValue(relationship.PublicRelationshipName));
            //_logger.LogInformation("-- propPublicNameId {0}", propInfo.GetValue((string.Concat(relationship.PublicRelationshipName, "Id"))));

            var propertyOfId = entity.GetType().GetProperty(string.Concat(propInfo.PropertyType.Name, "Id"));
            _logger.LogInformation("-- propertyOfId {0}", propertyOfId);

            var id = propertyOfId.GetValue(entity, null);
            _logger.LogInformation("-- id {0}", id);


            relationshipEntry.Data = new ResourceIdentifierObject {
                Id = id.ToString(),
                Type = relationship.PublicRelationshipName
            };

            if (Equals(relationship, _requestRelationship) || ShouldInclude(relationship, out relationshipChains)) {

                if ((relationshipChains != null && relationshipEntry.HasResource)) {
                    _logger.LogWarning("-- passed the if check: {0}, relationship != null: {1}, hasResource: {2}", relationshipChains != null && relationshipEntry.HasResource, relationshipChains != null, relationshipEntry.HasResource);
                    var svenList = new List<RelationshipAttribute> { relationship };

                    _logger.LogWarning("-- element of my list {0}", svenList[0].CanInclude);
                    relationshipChains.Add(svenList);
                    _logger.LogWarning("-- relationshipChains.Count {0}", relationshipChains.Count);

                    foreach (var chain in relationshipChains) {
                        // traverses (recursively) and extracts all (nested) related entities for the current inclusion chain.
                        _logger.LogWarning("-- chain ({0}) in relationship: {1}", relationshipChains.IndexOf(chain), chain);

                        foreach (var item in chain) {
                            _logger.LogWarning("-- item ({0}) in chain: {1}", chain.IndexOf(item), item);
                        }

                        _includedBuilder.IncludeRelationshipChain(chain, entity);
                    }
                }
            }



            var links = _linkBuilder.GetRelationshipLinks(relationship, entity);
            if (links != null)
                // if links relationshipLinks should be built for this entry, populate the "links" field.
                (relationshipEntry ??= new RelationshipEntry()).Links = links;

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
