using cube.api.eAxis.DTO.ProTrust.Data;
using cube.api.eAxis.REPO.Context;
using lib.cache.helper.Interface;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;

namespace Cube.Fx.Repo.Implementations
{
    public class AppCRUDV2
    {
        #region GetById Entity Based
        private DbContext _dbContext;
        private readonly string DefaultApplication = ConfigurationManager.AppSettings["DefaultApplication"];
        private readonly string ApplicationRepoAssemblyName = ConfigurationManager.AppSettings["ApplicationRepoAssemblyName"];
        private string _CtxName;        
        public static HashSet<string> SystemFilterCodes = new HashSet<string>();
        public static int MaxRowsToFetchIfPredicateNull = 0;
        public static bool IsSendNotificationMail = false;
        public static bool IsLogPredicate = false;
        public static bool IsRestrictFetchingAllRecords = false;
        public static int DbContextTimeOutSpan = 300;
        
        public virtual T GetById<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            if (predicate == null)
                return _dbContext.Set<T>().FirstOrDefault();
            else
                return _dbContext.Set<T>().FirstOrDefault<T>(predicate);
        }

        #endregion
        #region Delete Entity Based

        public virtual string Delete<T>(T TObject) where T : class
        {
            var entry = _dbContext.Entry(TObject);
            _dbContext.Set<T>().Attach(TObject);
            entry.State = System.Data.Entity.EntityState.Deleted;
            //*******DESV2************
            // LogQueue();
            _dbContext.SaveChanges();
            return "Success";
        }
        public virtual string Delete<T>(List<T> TListObject) where T : class
        {
            try
            {
                if (TListObject != null && TListObject.Count > 0)
                {
                    foreach (T TObject in TListObject)
                    {
                        var entry = _dbContext.Entry(TObject);
                        _dbContext.Set<T>().Attach(TObject);
                        entry.State = System.Data.Entity.EntityState.Deleted;
                    }
                }
                using (DbContextTransaction transaction = _dbContext.Database.BeginTransaction())
                {
                    //*******DESV2************
                    // LogQueue();
                    _dbContext.SaveChanges();
                    transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            finally { }
            return "Success";
        }
        public virtual DbContext DeleteTransaction<T>(T TObject) where T : class
        {
            var entry = _dbContext.Entry(TObject);
            _dbContext.Set<T>().Attach(TObject);
            entry.State = System.Data.Entity.EntityState.Deleted;
            //*******DESV2************
            // LogQueue();
            return _dbContext;
        }
        public virtual T DeleteTransaction<T>(T TObject, DbContext _Ctx, DbContextTransaction trx) where T : class
        {
            using (var _dbContext1 = SetTransaction(_Ctx, trx))
            {
                _dbContext1.Database.UseTransaction(trx.UnderlyingTransaction);
                var entry = _dbContext1.Entry(TObject);
                _dbContext1.Set<T>().Attach(TObject);
                entry.State = System.Data.Entity.EntityState.Deleted;
                //CallAudit();
                //*******DESV2************
                // LogQueue(_dbContext1);
                _dbContext1.SaveChanges();
                return TObject;
            }
        }
        public virtual List<T> DeleteListTransaction<T>(List<T> TListObject, DbContext _Ctx, DbContextTransaction trx) where T : class
        {
            if (TListObject != null && TListObject.Count > 0)
            {
                foreach (T TObject in TListObject)
                {
                    if (TObject != null)
                    {
                        var entry = _Ctx.Entry(TObject);
                        _Ctx.Set<T>().Attach(TObject);
                        entry.State = System.Data.Entity.EntityState.Deleted;
                        //*******DESV2************
                        // LogQueue(_Ctx);
                        _Ctx.SaveChanges();
                    }
                }
            }
            return TListObject;
        }

        #endregion
        public virtual DbContext SetTransaction(DbContext _Con, DbContextTransaction Trx)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name?.ToUpper() == ApplicationRepoAssemblyName);
            var contextTypeName = "cube.api.eAxis.REPO.Context.Context_" + _CtxName;

            DbContext dbContext;
            switch (_CtxName)
            {
                case "EA":
                    if (DefaultApplication.ToUpper() == "FX")
                    {
                        dbContext = new Context_FX(_Con.Database.Connection, false);
                    }
                    else
                    {
                        var contextType = assembly.GetType(contextTypeName);
                        dbContext = (DbContext)Activator.CreateInstance(contextType, _Con.Database.Connection, false);
                    }
                    break;
                // case "EA":
                //     dbContext = new Context_FX(_Con.Database.Connection, false);
                //     break;
                case "MD":
                    dbContext = new Context_FX(_Con.Database.Connection, false);
                    break;
                case "DE":
                    dbContext = new Context_DE(_Con.Database.Connection, false);
                    break;
                case "PR":
                    dbContext = new Context_PR(_Con.Database.Connection, false);
                    break;
                case "BP":
                    dbContext = new Context_BP(_Con.Database.Connection, false);
                    break;
                default:
                    dbContext = new Context_TC(_Con.Database.Connection, false);
                    break;

            }
            return dbContext;
        }
    }
}
