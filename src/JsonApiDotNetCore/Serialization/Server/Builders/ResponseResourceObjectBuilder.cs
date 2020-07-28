using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

            var pi = relationship.PropertyInfo;
            LogProperties(entity);
            LogAllowedRelationships(entity);

            // i really don't know why i checked if this is a collection? this can't be a collection wtf
            if (relationship is HasOneAttribute hasOneAttribute) {
                _logger.LogInformation("-- is hasOneAttribute");

                relationshipEntry = base.GetRelationshipData(relationship, entity);

                var id = entity.GetPropValue(string.Concat(pi.PropertyType.Name, "Id"));
                relationshipEntry.Data = new ResourceIdentifierObject {
                    Id = id.ToString(),
                    Type = relationship.PublicRelationshipName
                };
            } else if (relationship is HasManyAttribute hasManyAttribute) {
                _logger.LogInformation("-- is hasManyAttribute");
                relationshipEntry = base.GetRelationshipData(relationship, entity);
            } else {
                _logger.LogWarning("-- shouldn't be this, maybe its null: {0}", relationship);
            }

            if (Equals(relationship, _requestRelationship) || ShouldInclude(relationship, out relationshipChains)) {
                _logger.LogInformation("!! first step");
                relationshipEntry = base.GetRelationshipData(relationship, entity);

                if ((relationshipChains != null && relationshipEntry.HasResource)) {
                    _logger.LogInformation("!! second step");
                    foreach (var chain in relationshipChains) {
                        // traverses (recursively) and extracts all (nested) related entities for the current inclusion chain.
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

        private void LogAttributes() {
            if (_includedBuilder.GetIncluded()?.Count == 0) return;

            var attrs = _includedBuilder.GetIncluded()
                              .First().Attributes
                              .Select(x => $"{x.Key} - {x.Value}").Join("\n");
            _logger.LogInformation("\nAttributes of included:\n{0}\n", attrs);
        }

        private void LogChainAndChildren(List<List<RelationshipAttribute>> relationshipChains, List<RelationshipAttribute> chain) {
            _logger.LogWarning("-- chain ({0}) in relationship: {1}", relationshipChains.IndexOf(chain), chain);

            foreach (var item in chain) {
                _logger.LogWarning("-- item ({0}) in chain: {1}", chain.IndexOf(item), item);
            }
        }

        private void LogAfterIfCheck(RelationshipEntry relationshipEntry, List<List<RelationshipAttribute>> relationshipChains) {
            _logger.LogWarning("-- passed the if check: {0}, relationship != null: {1}, hasResource: {2}",
                                    relationshipChains != null && relationshipEntry.HasResource, relationshipChains != null,
                                    relationshipEntry.HasResource);
        }

        private void LogPropertyInfos(PropertyInfo pi) {
            _logger.LogInformation("-- propInfo: {0}", pi);
            _logger.LogInformation("-- propInfoName: {0}", pi.Name);
            _logger.LogInformation("-- propInfoType: {0}", pi.PropertyType);
            _logger.LogInformation("-- propInfoTypeName: {0}", pi.PropertyType.Name);
        }

        private void LogAllowedRelationships(IIdentifiable entity) {
            var allowedRelations = _fieldsToSerialize.GetAllowedRelationships(entity.GetType());
            var relOutput = allowedRelations.Select(x => x.PublicRelationshipName).Join("\n");
            _logger.LogInformation("\n{0}\n", relOutput);
        }

        private void LogProperties(object entity) {
            var output = entity.GetType().GetProperties()
                                .Select(x => $"{x.Name} - {entity.GetPropValue(x.Name)}")
                                .Join("\n");
            _logger.LogInformation("\n{0}\n", output);
        }

        private void LogParams(RelationshipAttribute relationship, IIdentifiable entity, RelationshipEntry relationshipEntry) {
            _logger.LogCritical("-- GetRelationshipData()");
            _logger.LogWarning("-- \nParams: \n relationship: {0}\n entity: {1}\n _requestRelationship: {2}\n relationshipEntry: {3}",
                relationship, entity, _requestRelationship, relationshipEntry);
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

    static class MyExtensions {

        public static string FirstCharToUpper(this string input) {
            if (string.IsNullOrEmpty(input))
                throw new ArgumentException("ARGH!");
            return input.First().ToString().ToUpper() + input.Substring(1);
        }

        public static bool IsNonStringEnumerable(this PropertyInfo pi) {
            return pi != null && pi.PropertyType.IsNonStringEnumerable();
        }

        public static bool IsNonStringEnumerable(this object instance) {
            return instance != null && instance.GetType().IsNonStringEnumerable();
        }

        public static bool IsNonStringEnumerable(this Type type) {
            if (type == null || type == typeof(string))
                return false;
            return typeof(IEnumerable).IsAssignableFrom(type);
        }

        public static object GetPropValue(this object obj, string name) {
            foreach (string part in name.Split('.')) {
                if (obj == null) { return null; }

                Type type = obj.GetType();
                PropertyInfo info = type.GetProperty(part);
                if (info == null) { return null; }

                obj = info.GetValue(obj, null);
            }
            return obj;
        }

        public static T GetPropValue<T>(this object obj, string name) {
            object retval = GetPropValue(obj, name);
            if (retval == null) { return default; }

            // throws InvalidCastException if types are incompatible
            return (T)retval;
        }
    }
}
