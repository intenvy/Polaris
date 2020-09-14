using Nest;
using System.Collections.Generic;
using System.Linq;

using Elastic.Validation;
using Elastic.Exceptions;
using Models;
using Models.ResponsePagination;

namespace Elastic.Communication.Nest
{
    public class NestElasticHandler<TModel> : IElasticHandler<TModel> where TModel : class, IModel
    {
        private static IElasticClient elasticClient = NestClientFactory.GetInstance().GetElasticClient();

        private void CheckIndex(string indexName, bool recreate)
        {
            try
            {
                ElasticIndexValidator.ValidateIndex(indexName);
            }
            catch(InvalidElasticIndexException e)
            {
                if(recreate)
                {
                    elasticClient.Indices.Delete(indexName);
                    elasticClient.Indices.Create(indexName, i => i.Map<TModel>(x => x.AutoMap()));
                    // Todo Custom mapping
                }
                else
                    throw e;
            }
        }

        public void Insert(TModel model, string indexName)
        {
            CheckIndex(indexName, false);
            var response = elasticClient.Index<TModel>(model, i => i.Index(indexName));
            ElasticResponseValidator.Validate(response);
        }

        public void BulkInsert(IEnumerable<TModel> models, string indexName)
        {
            CheckIndex(indexName, false);
            var bulk = new BulkDescriptor();
            foreach (var model in models)
            {
                bulk.Index<TModel>(x => x.Index(indexName).Document(model));
            }
            var response = elasticClient.Bulk(bulk);
            ElasticResponseValidator.Validate(response);
        }

        public IEnumerable<TModel> FetchAll(string indexName)
        {
            return FetchAllByQuery(new MatchAllQuery(), indexName);
        }

        protected ISearchResponse<TModel> RetrieveQueryResponse(
            QueryContainer container,
            string indexName,
            Pagination pagination = null
        )
        {
            var response = elasticClient.Search<TModel>(s => s
                .Index(indexName)
                .Query(q => container)
                .Size(pagination.PageSize)
                .From(pagination.PageIndex * pagination.PageSize));
            ElasticResponseValidator.Validate(response);
            return response;
        }

        public IReadOnlyCollection<IHit<TModel>> RetrieveQueryHits(
            QueryContainer container,
            string indexName,
            Pagination pagination = null
        )
        {
            var response = RetrieveQueryResponse(container, indexName, pagination);
            ElasticResponseValidator.Validate(response);
            return response.Hits;
        }

        public IEnumerable<TModel> RetrieveQueryDocuments(
            QueryContainer container,
            string indexName,
            Pagination pagination = null
        )
        {
            var response = RetrieveQueryResponse(container, indexName, pagination);
            ElasticResponseValidator.Validate(response);
            return response.Documents;
        }

        private ISearchResponse<TModel> NestScrollSearchInit(
            QueryContainer container,
            string indexName,
            string scrollTimeout = "2m",
            int scrollSize = 10000
        )
        {
            var response = elasticClient.Search<TModel>(s => s
                .Index(indexName)
                .From(0)
                .Take(scrollSize)
                .Query(q => container)
                .Scroll(scrollTimeout)
            );
            ElasticResponseValidator.Validate(response);
            return response;
        }

        protected void DeleteById_(string id_, string indexName)
        {
            var response = elasticClient.Delete<TModel>(id_, dd => dd.Index(indexName));
            ElasticResponseValidator.Validate(response);
        }

        protected void UpdateById_(string id_, string indexName, TModel newModel)
        {
            var response = elasticClient.Update<TModel>(id_, u => u.Index(indexName).Doc(newModel));
            ElasticResponseValidator.Validate(response);
        }

        public IEnumerable<TModel> FetchAllByQuery(QueryContainer queryContainer, string indexName)
        {
            var response = NestScrollSearchInit(queryContainer, indexName);
            var result = new List<TModel>();
            var anyDocumentLeft = true;
            string scrollId = response.ScrollId;
            while (anyDocumentLeft)
            {
                if (response.IsValid)
                {
                    result.AddRange(response.Documents);
                    scrollId = response.ScrollId;
                    response = elasticClient.Scroll<TModel>("2m", scrollId);
                    ElasticResponseValidator.Validate(response);
                }
                anyDocumentLeft = response.Documents.Any();
            }
            elasticClient.ClearScroll(new ClearScrollRequest(scrollId));
            return result;
        }
    }
}