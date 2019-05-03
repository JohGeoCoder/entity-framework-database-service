///
/// Author:         John George, .NET Web Applications Engineer
/// Github:         https://github.com/JohGeoCoder
/// Email:          john@nepaweb.solutions
/// This File:      https://github.com/JohGeoCoder/entity-framework-database-service
/// Create Date:    March 14, 2018
/// Update Date:    May 3, 2019
/// License: 		Attribution 4.0 International (CC BY 4.0)
/// You are free to:
///     Share - copy and redistribute the material in any medium or format.
///     
///     Adapt - remix, transform, and build upon the material
///     for any purpose, even commercially. 
///
/// Under the following terms:
///     Attribution - You must give appropriate credit, provide a link 
///     to the license, and indicate if changes were made. 
///     You may do so in any reasonable manner, but not in 
///     any way that suggests the licensor endorses you or 
///     your use.
///     
///     No additional restrictions - You may not apply legal terms or 
///     technological measures that legally restrict others from doing 
///     anything the license permits. 
/// 
/// Author Notes:
///     This class was a joy to make and is fun to maintain and upgrade. It started as a helper class to 
///     consolidate all the Entity Framework functionality into one central location. That remains its
///     primary purpose in my projects, but it has evolved into a very abstract and powerful tool that has
///     proven to speed up the development of all my projects.
///     
///     This DatabaseService is designed to be compatible with Entity Framework Core, Dependency Injection,
///     and Test Driven Development (TDD).
///     
///     Every once in a while, one of my projects will require that I implement a new feature to this 
///     DatabaseService class. So consider this a living tool, and check back often for upgrades! If 
///     you have a great idea, feel free to submit a pull request.
///     
/// Example Uses: 
///     public class AppointmentService : BaseRepoService<Appointment, ProjectDbContext> { }
///     
/// Features on the burner:
///     CreateAll(...)
///     DeleteAll(...)
///     UpsertAll(...)
///     
///     Considering a method call inversion. Instead of the CRUD methods being public and calling the private Call(TEntity, DatabaseAction)
///     method, the Call(TEntity, DatabaseAction) will be public and will call the private CRUD methods.
///

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace JohGeoCoder.Services.DatabaseService
{
    /// <summary>
    /// The Database Service is a generic abstract class that wraps all necessary Entity Framework logic, exceptions, and logging
    /// into a single location. It is easily droppable into any ASP.NET Core application to speed up development and to avoid
    /// peppering Entity Framework using statements around your application.
    /// 
    /// For each entity type, an Service class can be created that extends this abstract BaseRepoService class. An example is below:
    /// 
    /// public class UserService : BaseRepoService<User, MyDbContext>, IUserService<User> {...}
    /// 
    /// The above example represents how to create an repository service for an entity called User. The UserService implements the 
    /// BaseRepoService<TEntity, TDbContext> abstract class where TEntity is the User entity. The UserService can also implement an 
    /// IUserService<User> interface to define any more specific business logic that goes beyond the simple Get, Create, Update, or 
    /// Delete methods.
    /// </summary>
    /// 
    /// <typeparam name="TEntity">The entity generated from Entity Framework that we wish to interact with.</typeparam>
    /// <typeparam name="TDbContext">The DbContext implementation to work with.</typeparam>
    /// 
    public abstract class BaseRepoService<TEntity, TDbContext> : IRepoService<TEntity> where TEntity : class, IBaseEntity where TDbContext : DbContext
    {
        protected TDbContext _dbContext;

        /// <summary>
        ///     The DatabaseService's constructor is protected to ensure that only subclasses of the DatabaseService can instantiate the DatabaseService.
        /// </summary>
        /// <param name="dbContext">The database context, provided to the constructor ideally through dependency injection.</param>
        protected BaseRepoService(TDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        #region CRUD Methods

        /// <summary>
        ///     Enables the user to filter the repository of the entity type and include associated entities and collections
        ///     when retrieving from this repository. The weakness with this Get method is that the user cannot include
        ///     the properties of models within a collection. If that functionality is desired, then use the GetAllThenInclude(...) methods.
        /// </summary>
        /// <param name="filter">The filter function for the repository.</param>
        /// <param name="includeExpressions">
        ///     A collection of raw expressions that represent the nested entities and entity collections that will be included on the root entities.
        /// </param>
        /// <returns>
        ///     This method returns an IQueryable<T> that will contain the entities from the TEntity repository
        ///     that match the provided filter. If no filter was provided, all entities in the TEntity repository 
        ///     will be included.
        /// </returns>
        public IQueryable<TEntity> GetAll(Func<TEntity, bool> filter = null, params Expression<Func<TEntity, object>>[] includeExpressions)
        {
            IQueryable<TEntity> repository = _dbContext.Set<TEntity>();

            try
            {
                if (includeExpressions != null && includeExpressions.Any())
                {
                    foreach (var include in includeExpressions)
                    {
                        repository = repository.Include(include);
                    }
                }

                if (filter != null)
                {
                    repository = repository.Where(filter).AsQueryable();
                }
            }
            catch (Exception ex)
            {
                throw new BaseRepoServiceException("Error in DataService GetAll() method.", ex);
            }

            return repository;
        }

        /// <summary>
        ///     Enables the user to filter the repository of the entity type and include 
        ///     associated entities and collections when retrieving from this repository.
        ///     The weakness with this Get method is that the user cannot include the 
        ///     properties of models within a collection. If that functionality is desired, 
        ///     then use the GetAllThenInclude(...) methods.
        /// </summary>
        /// <param name="includeExpressions">
        ///     A collection of raw expressions that represent the nested entities and entity 
        ///     collections that will be included on the root entities.
        /// </param>
        /// <returns>
        ///     This method returns an IQueryable<T> that will contain the entities from the TEntity repository
        ///     that match the provided filter. If no filter was provided, all entities in the TEntity repository 
        ///     will be included.
        /// </returns>
        public IQueryable<TEntity> GetAll(params Expression<Func<TEntity, object>>[] includeExpressions)
        {
            return GetAll(null, includeExpressions);
        }

        /// <summary>
        ///     Enables the user to filter the repository of the entity type and include 
        ///     associated entities and collections when retrieving from this repository.
        ///     This method enables the developer to include properties of members of 
        ///     collections.
        /// </summary>
        /// <param name="filter">The filter function for the repository.</param>
        /// <param name="includeExpressions">
        ///     A collection of built expressions that represent the nested entities and entity collections that will be included on the root entities.
        /// </param>
        /// <returns>
        ///     This method returns an IQueryable<T> that will contain the entities from the TEntity repository
        ///     that match the provided filter. If no filter was provided, all entities in the TEntity repository 
        ///     will be included. The sub-properties specified in the built Include Expressions will be applied.
        /// </returns>
        public IQueryable<TEntity> GetAllThenInclude(Func<TEntity, bool> filter = null, params IncludeBuilderResult[] includeExpressions)
        {
            IQueryable<TEntity> repository = _dbContext.Set<TEntity>();

            try
            {
                if (includeExpressions != null && includeExpressions.Any())
                {
                    foreach (var expression in includeExpressions)
                    {
                        repository = repository.Include(expression.GetInclude());
                    }
                }

                if (filter != null)
                {
                    repository = repository.Where(filter).AsQueryable();
                }
            }
            catch (Exception ex)
            {
                throw new BaseRepoServiceException("Error in DataService GetAllThenInclude() method.", ex);
            }

            return repository;
        }

        /// <summary>
        ///     Enables the user to include associated entities and collections 
        ///     when retrieving from this repository. This method enables the 
        ///     developer to include properties of members of collections.
        /// </summary>
        /// <param name="includeExpressions">
        ///     A collection of built expressions that represent the nested entities and entity collections that will be included on the root entities.
        /// </param>
        /// <returns>
        ///     This method returns an IQueryable<T> that will contain the entities from the TEntity repository.
        ///     The sub-properties specified in the built Include Expressions will be applied.
        /// </returns>
        public IQueryable<TEntity> GetAllThenInclude(params IncludeBuilderResult[] includeExpressions)
        {
            return GetAllThenInclude(null, includeExpressions);
        }

        /// <summary>
        ///     This method creates a new entity in the TEntity repository.
        /// </summary>
        /// <param name="entity">The new entity to be created in the repository.</param>
        /// <returns>This method returns the freshly created entity from the repository.</returns>
        public async Task<TEntity> Create(TEntity entity)
        {
            var entityToCreate = PrepareCreate(entity);

            return await Call(entityToCreate, DatabaseAction.Create);
        }

        /// <summary>
        ///     This method updates an existing entity in the TEntity repository.
        /// </summary>
        /// <param name="entity">The existing entity with new information to be updated in the repository.</param>
        /// <returns>This method returns the freshly updated entity from the repository.</returns>
        public async Task<TEntity> Update(TEntity entity)
        {
            try
            {
                if (entity == null)
                {
                    throw new BaseRepoServiceException($"Entity is null when attempting to Update.");
                }

                var dbEntity = GetAll(e => e.Id == entity.Id).SingleOrDefault();

                if (dbEntity == null)
                {
                    throw new BaseRepoServiceException($"{entity.GetType()} with ID: {entity.Id} not found in the database when attempting to update.");
                }

                var entityToUpdate = PrepareUpdate(entity);
                return await Call(entityToUpdate, DatabaseAction.Update);
            }
            catch (BaseRepoServiceException ex)
            {
                throw ex;
            }
            catch (InvalidOperationException ex)
            {
                throw new BaseRepoServiceException($"There is more than one instance of the {entity.GetType()} with ID: {entity.Id}", ex);
            }
            catch (Exception ex)
            {
                throw new BaseRepoServiceException(ex);
            }
        }

        public async Task<IEnumerable<TEntity>> UpdateAll(IEnumerable<TEntity> entities)
        {
            if (entities == null || !entities.Any())
            {
                throw new BaseRepoServiceException($"Entity is null when attempting to Update.");
            }

            return await Call(entities, DatabaseAction.Update);
        }

        /// <summary>
        /// This method creates or updates the given entity, depending on an existence check.
        /// </summary>
        /// <param name="entity">The entity to be created or updated in the repository</param>
        /// <returns>This method returns the entity created or updated in the repository.</returns>
        public async Task<TEntity> Upsert(TEntity entity)
        {
            try
            {
                if (entity == null)
                {
                    throw new BaseRepoServiceException($"Entity is null when attempting to Upsert.");
                }

                Task<TEntity> action;
                if (entity.Id == 0L || !await Exists(e => e.Id == entity.Id))
                {
                    var entityToCreate = PrepareCreate(entity);
                    action = Call(entityToCreate, DatabaseAction.Create);
                }
                else
                {
                    var entityToUpdate = PrepareUpdate(entity);
                    action = Call(entityToUpdate, DatabaseAction.Update);
                }

                return action.GetAwaiter().GetResult();
            }
            catch (BaseRepoServiceException ex)
            {
                throw ex;
            }
            catch (InvalidOperationException ex)
            {
                throw new BaseRepoServiceException($"There is more than one instance of the {entity.GetType()} with ID: {entity.Id}", ex);
            }
            catch (Exception ex)
            {
                throw new BaseRepoServiceException(ex);
            }
        }

        /// <summary>
        ///     This method deletes an existing entity from the TEntity repository.
        /// </summary>
        /// <param name="entity">The existing entity to be deleted from the repository.</param>
        /// <returns>This method returns the entity deleted from the repository for any final processing.</returns>
        public async Task<TEntity> Delete(TEntity entity)
        {
            return await Call(entity, DatabaseAction.Delete);
        }

        /// <summary>
        ///     This method checks to see if there exists an entity in the TEntity repository that matches the given
        ///     predicate filter, if any.
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns>
        ///     This method returns true if there exists an entity in the TEntity repository that matches the given
        ///     predicate filter. If no predicate filter is provided, this method will return true if one or more
        ///     entities in the repository exists at all.
        /// </returns>
        public async Task<bool> Exists(Func<TEntity, bool> predicate = null)
        {
            IQueryable<TEntity> repository = _dbContext.Set<TEntity>();

            if (predicate == null)
            {
                return await Task.Run(() => repository.Any());
            }
            else
            {
                return await Task.Run(() => repository.Any(predicate));
            }
        }

        #endregion CRUD Calls

        #region Private Methods

        /// <summary>
        ///     Executes the desired repository action on the given entity.
        /// </summary>
        /// <param name="entity">The entity that contains data that will be persisted to the TEntity repository.</param>
        /// <param name="action">The type of action to be performed on the entity. The choices are Create, Update, or Delete</param>
        /// <returns></returns>
		private async Task<TEntity> Call(TEntity entity, DatabaseAction action)
        {
            return (await Call(new List<TEntity> { entity }, action)).FirstOrDefault();
        }

        private async Task<IEnumerable<TEntity>> Call(IEnumerable<TEntity> entities, DatabaseAction action)
        {
            try
            {
                var dbSet = _dbContext.Set<TEntity>();
                switch (action)
                {
                    case DatabaseAction.Create:
                        await dbSet.AddRangeAsync(entities);
                        break;
                    case DatabaseAction.Update:
                        dbSet.UpdateRange(entities);
                        break;
                    case DatabaseAction.Delete:
                        dbSet.RemoveRange(entities);
                        break;
                    default:
                        throw new BaseRepoServiceException($"Invalid database operation. {entities.FirstOrDefault()?.GetType().ToString() ?? "Items"} with IDs: {string.Join(", ", entities.Select(e => e.Id))}");
                }

                var countRowsAffected = await _dbContext.SaveChangesAsync();

                if (countRowsAffected > 0)
                {
                    //Allows database triggers to execute and return their data.
                    foreach (var entity in entities)
                    {
                        await _dbContext.Entry(entity).ReloadAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new BaseRepoServiceException($"Error on {action.ToString()} action on {entities.FirstOrDefault()?.GetType().ToString() ?? "Items"} with IDs: {string.Join(", ", entities.Select(e => e.Id))}", ex);
            }

            return entities;
        }

        #endregion Private Methods

        #region Abstract Methods

        /// <summary>
        ///     Allows the caller to apply any business logic to the entity before creating the new entity
        ///     in the TEntity repository.
        /// </summary>
        /// <param name="entity">The TEntity to be prepared for creation in the repository</param>
        /// <returns>
        ///     This method returns the TEntity with the business logic applied. 
        ///     There is no guarantee that the returned entity will be the same object reference, depending 
        ///     on how the caller overrides this method.
        /// </returns>
        protected virtual TEntity PrepareCreate(TEntity entity) { return entity; }

        /// <summary>
        ///     Allows the caller to apply any business logic to the entity before updating the existing entity
        ///     in the TEntity repository.
        /// </summary>
        /// <param name="entity">The TEntity to be prepared for updating in the repository</param>
        /// <returns>
        ///     This method returns the TEntity with the business logic applied. 
        ///     There is no guarantee that the returned entity will be the same object reference, depending 
        ///     on how the caller overrides this method.
        /// </returns>
        protected virtual TEntity PrepareUpdate(TEntity entity) { return entity; }

        #endregion Abstract Methods
    }

    public class BaseRepoServiceException : Exception
    {
        public BaseRepoServiceException() : this("An error occurred in the Database Service.") { }
        public BaseRepoServiceException(string message) : this(message, null) { }
        public BaseRepoServiceException(Exception innerException) : this("An error occurred in the Database Service.", innerException) { }
        public BaseRepoServiceException(string message, Exception innerException) : base(message, innerException) { }
    }

    public interface IRepoService<T> where T : class, IBaseEntity
    {
        IQueryable<TEntity> GetAll(Func<TEntity, bool> linqExpression = null, params Expression<Func<TEntity, object>>[] includeExpression);
        IQueryable<TEntity> GetAll(params Expression<Func<TEntity, object>>[] includeExpressions);
        IQueryable<TEntity> GetAllThenInclude(Func<TEntity, bool> linqExpression = null, params IncludeBuilderResult[] includeExpressions);
        IQueryable<TEntity> GetAllThenInclude(params IncludeBuilderResult[] includeExpressions);
        Task<TEntity> Create(TEntity entity);
        Task<TEntity> Update(TEntity entity);
        Task<TEntity> Upsert(TEntity entity);
        Task<IEnumerable<TEntity>> UpdateAll(IEnumerable<TEntity> entities);
        Task<TEntity> Delete(TEntity entity);
        Task<bool> Exists(Func<TEntity, bool> linqExpression = null);
    }

    public class IncludeBuilder<TEntity> where TEntity : IBaseEntity
    {
        public static IncludeBuilder<TEntity, Y> Include<Y>(Expression<Func<TEntity, Y>> initialInclude) where Y : IBaseEntity
        {
            return new IncludeBuilder<TEntity, Y>(initialInclude);
        }

        public static IncludeBuilder<TEntity, Y> Include<Y>(Expression<Func<TEntity, ICollection<Y>>> initialInclude) where Y : IBaseEntity
        {
            return new IncludeBuilder<TEntity, Y>(initialInclude);
        }
    }

    public class IncludeBuilder<TEntity, YEntity> where TEntity : IBaseEntity where YEntity : IBaseEntity
    {
        private string IncludeExpression = "";

        public IncludeBuilder(Expression<Func<TEntity, YEntity>> initialInclude)
        {
            IncludeExpression = initialInclude.Body.Type.Name;
        }

        public IncludeBuilder(Expression<Func<TEntity, ICollection<YEntity>>> initialInclude)
        {
            IncludeExpression = initialInclude.Body.Type.GenericTypeArguments[0].Name;
        }

        private IncludeBuilder(string previousExpression, string addition)
        {
            IncludeExpression = new StringBuilder().Append(previousExpression).Append(".").Append(addition).ToString();
        }

        public IncludeBuilder<YEntity, Z> ThenInclude<Z>(Expression<Func<YEntity, Z>> nextExpression) where Z : IBaseEntity
        {
            var includeName = nextExpression.Body.Type.Name;
            return new IncludeBuilder<YEntity, Z>(IncludeExpression, includeName);
        }

        public IncludeBuilder<YEntity, Z> ThenInclude<Z>(Expression<Func<YEntity, ICollection<Z>>> nextExpression) where Z : IBaseEntity
        {
            var includeName = nextExpression.Body.Type.GenericTypeArguments[0].Name;
            return new IncludeBuilder<YEntity, Z>(IncludeExpression, includeName);
        }

        public IncludeBuilderResult Done()
        {
            return new IncludeBuilderResult(IncludeExpression);
        }
    }

    public class IncludeBuilderResult
    {
        private string IncludeExpression = "";

        public IncludeBuilderResult(string includeExpression)
        {
            IncludeExpression = includeExpression;
        }

        public string GetInclude()
        {
            return IncludeExpression;
        }
    }

    public interface IBaseEntity
    {
        long Id { get; set; }
    }

    public enum DatabaseAction
    {
        Create,
        Update,
        Delete
    }
}
