///
/// Author:         John George, .NET Web Applications Engineer
/// Github:         https://github.com/JohGeoCoder
/// Email:          john@nepaweb.solutions
/// Create Date:    March 14, 2018
/// Update Date:    December 18, 2018
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
/// Features on the burner:
///     CreateAll(...)
///     UpdateAll(...)
///     DeleteAll(...)
///     Upsert(...)
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
    public abstract class DatabaseService<TEntity, TContext> : IDatabaseService<TEntity> where TEntity : class, IBaseModel where TContext : DbContext
    {
        protected TContext _dbContext;

        /// <summary>
        ///     The DatabaseService's constructor is protected to ensure that only subclasses of the DatabaseService can instantiate the DatabaseService.
        /// </summary>
        /// <param name="dbContext">The database context, provided to the constructor ideally through dependency injection.</param>
        protected DatabaseService(TContext dbContext)
        {
            _dbContext = dbContext;
        }

        #region CRUD Methods

        /// <summary>
        ///     Enables the caller to filter the repository of the entity type.
        /// </summary>
        /// <param name="predicate">The predicate that will act as a filter for the repository.</param>
        /// <param name="includeExpressions">
        ///     A collection of Expressions that will be iterated over and "included" in the Linq query.
        ///     Alternatively, this GetAll method returns an IQueryable interface, so the caller can handle
        ///     the Includes.
        /// </param>
        /// <returns>
        ///     This method returns an IQueryable<T> that will contain the entities from the TEntity repository
        ///     that match the provided filter. 
        ///     If no filter was provided, all entities in the TEntity repository will be included.
        /// </returns>
        public IQueryable<TEntity> GetAll(Func<TEntity, bool> predicate = null, params Expression<Func<TEntity, object>>[] includeExpressions)
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

                if (predicate != null)
                {
                    repository = repository.Where(predicate).AsQueryable();
                }
            }
            catch (Exception ex)
            {
                throw new DatabaseServiceException("Error in DataService GetAll() method.", ex);
            }

            return repository;
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
                    throw new DatabaseServiceException($"Entity is null when attempting to Update.");
                }

                var dbEntity = GetAll(e => e.Id == entity.Id).SingleOrDefault();

                if (dbEntity == null)
                {
                    throw new DatabaseServiceException($"{entity.GetType()} with ID: {entity.Id} not found in the database when attempting to update.");
                }

                var entityToUpdate = PrepareUpdate(entity);
                return await Call(entityToUpdate, DatabaseAction.Update);
            }
            catch (DatabaseServiceException ex)
            {
                throw ex;
            }
            catch (InvalidOperationException ex)
            {
                throw new DatabaseServiceException($"There is more than one instance of the {entity.GetType()} with ID: {entity.Id}", ex);
            }
            catch (Exception ex)
            {
                throw new DatabaseServiceException(ex);
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
            TEntity pureEntity = null;

            try
            {
                var dbSet = _dbContext.Set<TEntity>();
                switch (action)
                {
                    case DatabaseAction.Create:
                        dbSet.Add(entity);
                        break;
                    case DatabaseAction.Update:
                        dbSet.Update(entity);
                        break;
                    case DatabaseAction.Delete:
                        entity.Deleted = true; //Specific logic for LongLifeFleet
                        dbSet.Update(entity);
                        break;
                    default:
                        throw new DatabaseServiceException($"Invalid database operation. {entity.GetType()} with ID: {entity.Id}");
                }

                var countRowsAffected = await _dbContext.SaveChangesAsync();

                if (countRowsAffected > 0)
                {
                    if (DatabaseAction.Delete == action)
                    {
                        pureEntity = entity;
                    }
                    else
                    {
                        pureEntity = await dbSet.FindAsync(entity.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new DatabaseServiceException($"Error on {action.ToString()} action on {entity.GetType()} ID: {entity.Id}", ex);
            }

            return pureEntity;
        }

        private IEnumerable<string> GetAllNavigations(Type type, int maxLevel, string parentPath = "")
        {
            var navigationPaths = new List<string>();
            if (type == null)
            {
                return navigationPaths;
            }

            if (!string.IsNullOrWhiteSpace(parentPath) && parentPath.Where(c => c == '.').Count() == (maxLevel - 1))
            {
                return navigationPaths;
            }

            IEnumerable<INavigation> navigations = null;

            Type effectiveType = type;

            //If current type is a collection.
            if (type.GenericTypeArguments.Any())
            {
                effectiveType = type.GenericTypeArguments.First();
            }

            navigations = _dbContext.Model.FindEntityType(effectiveType).GetNavigations();
            if (navigations == null || !navigations.Any()) return navigationPaths;

            //Recursively add the navigation paths.
            foreach (var property in navigations)
            {
                var newPath = string.IsNullOrWhiteSpace(parentPath)
                    ? property.Name
                    : string.Join(".", parentPath, property.Name);

                //Add this current navigation property.
                navigationPaths.Add(newPath);

                var propertyType = property.ClrType;

                //Add this navigation property's children navigation properties.
                navigationPaths.AddRange(GetAllNavigations(propertyType, maxLevel, newPath));
            }

            return navigationPaths;
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

    public class DatabaseServiceException : Exception
    {
        public DatabaseServiceException() : this("An error occurred in the Database Service.") { }
        public DatabaseServiceException(string message) : this(message, null) { }
        public DatabaseServiceException(Exception innerException) : this("An error occurred in the Database Service.", innerException) { }
        public DatabaseServiceException(string message, Exception innerException) : base(message, innerException) { }
    }

    public interface IDatabaseService<T> where T : class, IBaseModel
    {
        IQueryable<T> GetAll(Func<T, bool> linqExpression = null, params Expression<Func<T, object>>[] includeExpression);
        Task<T> Create(T entity);
        Task<T> Update(T entity);
        Task<T> Delete(T entity);
        Task<bool> Exists(Func<T, bool> linqExpression = null);
    }

    public interface IBaseModel
    {
        long Id { get; set; }
        bool Deleted { get; set; }
    }

    public enum DatabaseAction
    {
        Create,
        Update,
        Delete
    }
}
