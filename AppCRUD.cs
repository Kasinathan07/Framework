using cube.api.eAxis.DTO;
using cube.api.eAxis.DTO.ProTrust.Data;
using cube.api.eAxis.REPO.Context;
using cube.api.eAxis.REPO.Factory;
using cube.api.eAxis.REPO.Helpers;
using cube.api.eAxis.REPO.Helpers.Factory;
using cube.api.eAxis.REPO.Interface;
using Lib.Helper.CommonHelpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using cube.api.eAxis.REPO.Model;
using cube.api.eAxis.REPO.Implementations.DesV2;
using System.Text;
using cube.api.eAxis.UI;
using System.Diagnostics;
using System.Transactions;
using Cube.Fx.Repo.Context;
using Cube.Fx.Helper.CommonHelpers;
using Cube.Fx.Repo.Implementations;
using System.ComponentModel.DataAnnotations.Schema;
using Cube.Fx.Helper.Extensions;
using lib.cache.helper.Interface;
using lib.cache.helper.Factory;

namespace cube.api.eAxis.REPO.Implementations
{
    public class AppCRUD : ICRUD
    {
        #region Repo Context Details

        private DbContext _dbContext;
        private readonly string DefaultApplication = ConfigurationManager.AppSettings["DefaultApplication"];
        private readonly string ApplicationRepoAssemblyName = ConfigurationManager.AppSettings["ApplicationRepoAssemblyName"];
        private string _CtxName;
        List<DataEvent> DataEvents = new List<DataEvent>();
        public static HashSet<string> SystemFilterCodes = new HashSet<string>();
        public static int MaxRowsToFetchIfPredicateNull = 0;
        public static bool IsSendNotificationMail = false;
        public static bool IsLogPredicate = false;
        public static bool IsRestrictFetchingAllRecords = false;
        public static int DbContextTimeOutSpan = 300;
        private ICacheService CacheService;
        private object _isRedisCacheEnabled;
        private string _environment;

        public AppCRUD(string ContextName)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name?.ToUpper() == ApplicationRepoAssemblyName);

            if (string.IsNullOrEmpty(ContextName))
            {
                TokenClaims tc = TrustHelper.GetTokenCliams();
                if (tc != null)
                {
                    ContextName = tc.ApplicationId;
                    _CtxName = ContextName;
                }
                else
                    ContextName = _CtxName;
            }


            switch (ContextName)
            {
                case "EA":
                    {
                        if (!string.IsNullOrEmpty(DefaultApplication) && DefaultApplication.ToUpper() == "FX")
                        {
                            _dbContext = new Context_FX();
                        }
                        else
                        {
                            Debug.WriteLine("Assembly Name: " + this.GetType().AssemblyQualifiedName);
                            _dbContext = (DbContext)Activator.CreateInstance(assembly.GetType("cube.api.eAxis.REPO.Context.Context_" + ContextName));
                        }

                        IncludeBaseAndDerivedDbSetProperties();
                        
                    }
                    break;
                case "FX":
                    _dbContext = new Context_FX();
                    break;
                case "MD":
                    _dbContext = new Context_FX();
                    break;
                case "DE":
                    _dbContext = new Context_DE();
                    break;
                case "PR":
                    _dbContext = new Context_PR();
                    break;
                case "BP":
                    _dbContext = new Context_BP();
                    break;
                case "EA_R":
                    {
                        if (DefaultApplication.ToUpper() == "FX")
                        {
                            _dbContext = new Context_FX_R();
                        }
                        else
                        {
                            Debug.WriteLine("Assembly Name: " + this.GetType().AssemblyQualifiedName);
                            _dbContext = (DbContext)Activator.CreateInstance(assembly.GetType("cube.api.eAxis.REPO.Context.Context_" + ContextName));
                        }

                        IncludeBaseAndDerivedDbSetProperties();
                    }
                    break;
                case "TC_R":
                    _dbContext = new Context_TC_R();
                    break;
                case "DE_R":
                    _dbContext = new Context_DE_R();
                    break;
                case "EXT":
                    _dbContext = new Context_EXT();
                    break;
                case "LOG":
                    _dbContext = new Context_Logging();
                    break;
                case "RPT_R":
                    _dbContext = new Context_Reports_R();
                    break;
                case "EF":
                    {
                        Debug.WriteLine("Assembly Name: " + this.GetType().AssemblyQualifiedName);
                        _dbContext = (DbContext)Activator.CreateInstance(assembly.GetType("cube.api.eAxis.REPO.Context.Context_" + ContextName));

                        IncludeBaseAndDerivedDbSetProperties();
                    }
                    break;
                case "RPT":
                    _dbContext = new Context_Reports();
                    break;
                default:
                    _dbContext = new Context_TC();
                    break;
            }
            if (string.IsNullOrEmpty(_CtxName) && !string.IsNullOrEmpty(ContextName))
            {
                _CtxName = ContextName;
            }
            //SERIALIZE WILL FAIL WITH PROXIED ENTITIES
            _dbContext.Configuration.ProxyCreationEnabled = false;
            //ENABLING COULD CAUSE ENDLESS LOOPS AND PERFORMANCE PROBLEMS
            _dbContext.Configuration.LazyLoadingEnabled = false;
            // TIME OUT SPAN
            _dbContext.Database.CommandTimeout = AppCRUD.DbContextTimeOutSpan;
        }
        public AppCRUD(DbContext context)
        {
            _dbContext = context;
            _dbContext.Configuration.ProxyCreationEnabled = false;
            _dbContext.Configuration.LazyLoadingEnabled = false;
        }
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
        public DbContext GetContext()
        {
            try
            {
                return _dbContext;
            }
            catch (Exception ex)
            {

                throw;
            }

        }
        public void Dispose()
        {
            if (_dbContext != null)
            {

                var connection = _dbContext.Database.Connection;
                connection.Dispose();
                _dbContext.Dispose();
            }
            System.GC.SuppressFinalize(this);
        }
        public void IncludeBaseAndDerivedDbSetProperties()
        {
            // Include DbSet properties from base class
            var baseType = _dbContext.GetType().BaseType;
            if (baseType != null)
            {
                var dbSetProperties = baseType.GetProperties()
                    .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>));

                foreach (var property in dbSetProperties)
                {
                    var entityType = property.PropertyType.GetGenericArguments().First();
                    var setMethod = typeof(DbContext).GetMethods()
                        .FirstOrDefault(m => m.Name == "Set" && m.GetParameters().Length == 0)
                        ?.MakeGenericMethod(entityType);

                    setMethod?.Invoke(_dbContext, null);
                }
            }

            // Include DbSet properties from derived class
            var derivedType = _dbContext.GetType();
            var derivedDbSetProperties = derivedType.GetProperties()
                .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>));

            foreach (var property in derivedDbSetProperties)
            {
                var entityType = property.PropertyType.GetGenericArguments().First();
                var setMethod = typeof(DbContext).GetMethods()
                    .FirstOrDefault(m => m.Name == "Set" && m.GetParameters().Length == 0)
                    ?.MakeGenericMethod(entityType);

                setMethod?.Invoke(_dbContext, null);
            }
        }
        public T GetInstance<T>(string type)
        {
            return (T)Activator.CreateInstance(Type.GetType(type));
        }

        #endregion

        #region Insert Entity Based

        public virtual T Insert<T>(T TObject) where T : class
        {
            var des = new RuleEngine();

            var TObj = AddVersionProperty<T>(TObject);
            if (CheckTenantValid<T>(TObject))
                TObj = AddTenentCodeProperty<T>(TObj);
            TObj = AddProperty<T>(TObj, "CreatedBy,UploadedBy,ModifiedBy");
            TObj = AddDateTimeBasedOnProperty<T>(TObj, "CreatedDateTime,ModifiedDateTime");
            //TObj = SetPropertyNull<T>(TObj, RestrictPropertyName<T>(TObj, "ModifiedBy,ModifiedDateTime"));
            var newEntry = _dbContext.Set<T>().Add(TObj);
            //*******DESV2************
            if (!string.IsNullOrEmpty(LogQueueHelper.GetTableName(TObject)))
            {
                #region DES PreProcessing
                //Starting the DES preprocessing which will check if there is any fields configured for events asynchronously and returns the changed fields
                var desEntity = new DesEntity();
                desEntity.Context = _dbContext;
                // desEntity.transaction = trx;
                desEntity.TenantCode = GetTenantCode();
                desEntity.Entity = TObject;
                desEntity.Action = "I";

                des.desEntity = desEntity;
                des.PreProcessing(TObject);
                //Task<Dictionary<string, object>> desPreProcessingTask = Task.Factory.StartNew(()=> des.PreProcessing(TObject));
                #endregion
            }
            #region Invoke Des Process
            //*******DESV2************
            //Wait for the DES Preprocessing to complete and get the changed fields. If there are any fields then call the DES Process
            //desPreProcessingTask.Wait();
            var desEvents = des.Execute();
            if (desEvents != null && desEvents.Count > 0)
                TrustHelper.SetObjectInRequestSession<DesEvent>("DesEvents", desEvents);
            #endregion

            // LogQueue();
            _dbContext.SaveChanges();
            if (!string.IsNullOrEmpty(LogQueueHelper.GetTableName(TObject)))
            {
                PublishEvent();
            }
            return newEntry;
        }
        private void PrepareBulkInsert<T>(List<T> TListObject) where T : class
        {
            try
            {
                if (TListObject != null && TListObject.Count > 0)
                {
                    foreach (T TObject in TListObject)
                    {
                        var TObj = AddVersionProperty<T>(TObject);
                        if (CheckTenantValid<T>(TObject))
                            TObj = AddTenentCodeProperty<T>(TObj);
                        TObj = AddProperty<T>(TObj, "CreatedBy,UploadedBy,ModifiedBy");
                        TObj = AddDateTimeBasedOnProperty<T>(TObj, "CreatedDateTime,ModifiedDateTime");
                        //TObj = SetPropertyNull<T>(TObj, RestrictPropertyName<T>(TObj, "ModifiedBy,ModifiedDateTime"));
                        _dbContext.Set<T>().Add(TObj);
                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        public virtual List<T> Insert<T>(List<T> TListObject) where T : class
        {
            PrepareBulkInsert(TListObject);
            using (DbContextTransaction transaction = _dbContext.Database.BeginTransaction())
            {
                string tableName = LogQueueHelper.GetTableName(TListObject.FirstOrDefault());
                if (!string.IsNullOrEmpty(tableName))
                {
                    TListObject.ForEach(ent =>
                    {
                        #region DES PreProcessing
                        //Starting the DES preprocessing which will check if there is any fields configured for events asynchronously and returns the changed fields
                        var desEntity = new DesEntity();
                        desEntity.Context = _dbContext;
                        desEntity.transaction = transaction;
                        desEntity.TenantCode = GetTenantCode();
                        desEntity.Entity = ent;
                        desEntity.Action = "I";
                        var des = new RuleEngine();
                        des.desEntity = desEntity;
                        des.PreProcessing(ent, TListObject, tableName);
                        //Task<Dictionary<string, object>> desPreProcessingTask = Task.Factory.StartNew(()=> des.PreProcessing(TObject));
                        #endregion

                        //*******DESV2************
                        #region Invoke Des Process
                        //*******DESV2************
                        //Wait for the DES Preprocessing to complete and get the changed fields. If there are any fields then call the DES Process
                        //desPreProcessingTask.Wait();
                        var desEvents = des.Execute();
                        if (desEvents != null && desEvents.Count > 0)
                            TrustHelper.SetObjectInRequestSession<DesEvent>("DesEvents", desEvents);
                        #endregion
                    });
                }
                // LogQueue();
                _dbContext.SaveChanges();
                transaction.Commit();

            }
            if (!string.IsNullOrEmpty(LogQueueHelper.GetTableName(TListObject.FirstOrDefault())))
            {
                PublishEvent();
            }
            return TListObject;
        }
        public virtual List<T> Insert<T>(List<T> TListObject, DbContextTransaction transaction) where T : class
        {
            PrepareBulkInsert(TListObject);
            using (transaction)
            {
                //*******DESV2************
                // LogQueue();
                _dbContext.SaveChanges();
            }
            return TListObject;
        }
        public virtual DbContext InsertTransaction<T>(T TObject) where T : class
        {
            var TObj = AddVersionProperty<T>(TObject);
            if (CheckTenantValid<T>(TObject))
                TObj = AddTenentCodeProperty<T>(TObj);
            TObj = AddProperty<T>(TObj, "CreatedBy,UploadedBy,ModifiedBy");
            TObj = AddDateTimeBasedOnProperty<T>(TObj, "CreatedDateTime,ModifiedDateTime");
            //TObj = SetPropertyNull<T>(TObj, RestrictPropertyName<T>(TObj, "ModifiedBy,ModifiedDateTime"));
            _dbContext.Set<T>().Add(TObj);
            //*******DESV2************
            if (!string.IsNullOrEmpty(LogQueueHelper.GetTableName(TObject)))
            {
                #region DES PreProcessing
                //Starting the DES preprocessing which will check if there is any fields configured for events asynchronously and returns the changed fields
                var desEntity = new DesEntity();
                desEntity.Context = _dbContext;
                //desEntity.transaction = trx;
                desEntity.TenantCode = GetTenantCode();
                desEntity.Entity = TObject;
                desEntity.Action = "I";
                var des = new RuleEngine();
                des.desEntity = desEntity;
                des.PreProcessing(TObject);
                //Task<Dictionary<string, object>> desPreProcessingTask = Task.Factory.StartNew(()=> des.PreProcessing(TObject));
                #endregion



                #region Invoke Des Process
                //*******DESV2************
                //Wait for the DES Preprocessing to complete and get the changed fields. If there are any fields then call the DES Process
                //desPreProcessingTask.Wait();
                var desEvents = des.Execute();
                if (desEvents != null && desEvents.Count > 0)
                    TrustHelper.SetObjectInRequestSession<DesEvent>("DesEvents", desEvents);
                #endregion
            }
            // LogQueue(_dbContext);
            return _dbContext;
        }
        public virtual T InsertTransaction<T>(T TObject, DbContext _Ctx, DbContextTransaction trx) where T : class
        {

            using (var _dbContext1 = SetTransaction(_Ctx, trx))
            {
                SharedEntity sharedEntity = new SharedEntity();
                _dbContext1.Database.UseTransaction(trx.UnderlyingTransaction);
                var TObj = AddVersionProperty<T>(TObject);
                if (CheckTenantValid<T>(TObject))
                    TObj = AddTenentCodeProperty<T>(TObj);
                TObj = AddProperty<T>(TObj, "CreatedBy,UploadedBy,ModifiedBy");
                TObj = AddDateTimeBasedOnProperty<T>(TObj, "CreatedDateTime,ModifiedDateTime");
                //TObj = SetPropertyNull<T>(TObj, RestrictPropertyName<T>(TObj, "ModifiedBy,ModifiedDateTime"));
                _dbContext1.Set<T>().Add(TObj);
                sharedEntity.SaveJobSharedEntityDetails(TObject, _Ctx, trx, "I");

                #region Invoke Des Process
                //*******DESV2************
                #region DES PreProcessing
                //Starting the DES preprocessing which will check if there is any fields configured for events asynchronously and returns the changed fields
                var desEntity = new DesEntity();
                desEntity.Context = _Ctx;
                desEntity.transaction = trx;
                desEntity.TenantCode = GetTenantCode();
                desEntity.Entity = TObject;
                desEntity.Action = "I";
                var des = new RuleEngine();
                des.desEntity = desEntity;
                des.PreProcessing(TObject);
                //Task<Dictionary<string, object>> desPreProcessingTask = Task.Factory.StartNew(()=> des.PreProcessing(TObject));
                #endregion


                var desEvents = des.Execute();
                if (desEvents != null && desEvents.Count > 0)
                    TrustHelper.SetObjectInRequestSession<DesEvent>("DesEvents_Transaction", desEvents);
                #endregion

                // LogQueue(_dbContext1, trx);
                _dbContext1.SaveChanges();

                return TObject;
            }
        }
        public virtual T Insert<T>(T TObject, DbContext _Ctx, DbContextTransaction transaction) where T : class
        {
            Int32 sharedEntityCount = 0;
            SharedEntity objSharedEntity = new SharedEntity();
            List<DataSharedEntity> listSharedEntity = new List<DataSharedEntity>();
            if (TObject != null)
            {
                var TObj = AddVersionProperty<T>(TObject);
                if (CheckTenantValid<T>(TObject))
                    TObj = AddTenentCodeProperty<T>(TObj);
                TObj = AddProperty<T>(TObj, "CreatedBy,UploadedBy,ModifiedBy");
                TObj = AddDateTimeBasedOnProperty<T>(TObj, "CreatedDateTime,ModifiedDateTime//");
                //TObj = SetPropertyNull<T>(TObj, RestrictPropertyName<T>(TObj, "ModifiedBy,ModifiedDateTime"));
                _Ctx.Set<T>().Add(TObj);
            }
            sharedEntityCount = CheckSharedEntityCount(TObject);
            if (sharedEntityCount > 0)
                listSharedEntity = objSharedEntity.PrepareJobSharedEntityDetails(TObject, _Ctx, transaction, "I");

            //*******DESV2************
            if (!string.IsNullOrEmpty(LogQueueHelper.GetTableName(TObject)))
            {
                #region DES PreProcessing
                //Starting the DES preprocessing which will check if there is any fields configured for events asynchronously and returns the changed fields
                var desEntity = new DesEntity();
                desEntity.Context = _Ctx;
                //desEntity.transaction = trx;
                desEntity.TenantCode = GetTenantCode();
                desEntity.Entity = TObject;
                desEntity.Action = "I";
                var des = new RuleEngine();
                des.desEntity = desEntity;
                des.PreProcessing(TObject);
                //Task<Dictionary<string, object>> desPreProcessingTask = Task.Factory.StartNew(()=> des.PreProcessing(TObject));
                #endregion

                #region Invoke Des Process
                //*******DESV2************
                //Wait for the DES Preprocessing to complete and get the changed fields. If there are any fields then call the DES Process
                //desPreProcessingTask.Wait();
                var desEvents = des.Execute();

                if (desEvents != null && desEvents.Count > 0)
                    TrustHelper.SetObjectInRequestSession<DesEvent>("DesEvents", desEvents);
                #endregion
            }
            // LogQueue(_Ctx, transaction);
            _Ctx.SaveChanges();
            if (!string.IsNullOrEmpty(LogQueueHelper.GetTableName(TObject)))
            {
                PublishEvent();
            }
            return TObject;
        }
        public virtual List<T> InsertListTransaction<T>(List<T> TListObject, DbContext _Ctx, DbContextTransaction transaction) where T : class
        {
            Int32 sharedEntityCount = 0;
            SharedEntity objSharedEntity = new SharedEntity();
            List<DataSharedEntity> listSharedEntity = new List<DataSharedEntity>();

            if (TListObject != null && TListObject.Count > 0)
            {
                foreach (T TObject in TListObject)
                {
                    if (TObject != null)
                    {
                        var TObj = AddVersionProperty<T>(TObject);
                        if (CheckTenantValid<T>(TObject))
                            TObj = AddTenentCodeProperty<T>(TObj);
                        TObj = AddProperty<T>(TObj, "CreatedBy,UploadedBy,ModifiedBy");
                        TObj = AddDateTimeBasedOnProperty<T>(TObj, "CreatedDateTime,ModifiedDateTime");
                        //TObj = SetPropertyNull<T>(TObj, RestrictPropertyName<T>(TObj, "ModifiedBy,ModifiedDateTime"));
                        _Ctx.Set<T>().Add(TObj);
                    }
                    sharedEntityCount = CheckSharedEntityCount(TObject);
                    if (sharedEntityCount > 0)
                        listSharedEntity = objSharedEntity.PrepareJobSharedEntityDetails(TObject, _Ctx, transaction, "I");

                    if (TObject != null && !string.IsNullOrEmpty(LogQueueHelper.GetTableName(TObject)))
                    {
                        #region DES PreProcessing
                        //Starting the DES preprocessing which will check if there is any fields configured for events asynchronously and returns the changed fields
                        var desEntity = new DesEntity();
                        desEntity.Context = _Ctx;
                        desEntity.transaction = transaction;
                        desEntity.TenantCode = GetTenantCode();
                        desEntity.Entity = TObject;
                        desEntity.Action = "I";
                        var des = new RuleEngine();
                        des.desEntity = desEntity;
                        des.PreProcessing(TObject);
                        //Task<Dictionary<string, object>> desPreProcessingTask = Task.Factory.StartNew(()=> des.PreProcessing(TObject));
                        #endregion



                        #region Invoke Des Process
                        //*******DESV2************
                        //Wait for the DES Preprocessing to complete and get the changed fields. If there are any fields then call the DES Process
                        //desPreProcessingTask.Wait();
                        var desEvents = des.Execute();
                        if (desEvents != null && desEvents.Count > 0)
                            TrustHelper.SetObjectInRequestSession<DesEvent>("DesEvents_Transaction", desEvents);
                        #endregion
                    }
                }
            }
            //*******DESV2************
            // LogQueue(_Ctx, transaction);
            _Ctx.SaveChanges();
            return TListObject;
        }

        #endregion

        #region Update Entity Based

        public virtual T Update<T>(T TObject) where T : class
        {
            var TObj = AddVersionProperty<T>(TObject);
            TObj = AddTenentCodeProperty<T>(TObj);
            TObj = AddProperty<T>(TObj, "ModifiedBy,DeletedBy");
            TObj = AddDateTimeBasedOnProperty<T>(TObj, "ModifiedDateTime");
            var entry = _dbContext.Entry(TObj);
            _dbContext.Set<T>().Attach(TObj);
            entry.State = System.Data.Entity.EntityState.Modified;
            if (entry.Entity.GetType().GetProperty("TNT_TenantCode") != null)
            {
                entry.Property("TNT_TenantCode").IsModified = false;
            }
            else if (entry.Entity.GetType().GetProperty("CMN_TenantCode") != null)
            {
                entry.Property("CMN_TenantCode").IsModified = false;
            }
            foreach (var item in RestrictPropertyName(TObj, "CreatedBy,CreatedDateTime"))
            {
                if (entry.Entity.GetType().GetProperty(item) != null)
                {
                    entry.Property(item).IsModified = false;
                }
            }
            if (!string.IsNullOrEmpty(LogQueueHelper.GetTableName(TObject)))
            {
                //*******DESV2************
                #region DES PreProcessing
                //Starting the DES preprocessing which will check if there is any fields configured for events asynchronously and returns the changed fields
                var desEntity = new DesEntity();
                desEntity.Context = _dbContext;
                //desEntity.transaction = trx;
                desEntity.TenantCode = GetTenantCode();
                desEntity.Entity = TObject;
                desEntity.Action = "U";
                var des = new RuleEngine();
                des.desEntity = desEntity;
                des.PreProcessing(TObject);
                //Task<Dictionary<string, object>> desPreProcessingTask = Task.Factory.StartNew(()=> des.PreProcessing(TObject));
                #endregion

                #region Invoke Des Process
                //*******DESV2************
                //Wait for the DES Preprocessing to complete and get the changed fields. If there are any fields then call the DES Process
                //desPreProcessingTask.Wait();
                var desEvents = des.Execute();
                if (desEvents != null && desEvents.Count > 0)
                    TrustHelper.SetObjectInRequestSession<DesEvent>("DesEvents", desEvents);
                #endregion
            }
            // LogQueue();
            _dbContext.SaveChanges();
            if (!string.IsNullOrEmpty(LogQueueHelper.GetTableName(TObject)))
            {
                PublishEvent();
            }
            return TObject;
        }
        private void PrepareBulkUpdate<T>(List<T> TListObject) where T : class
        {
            try
            {
                if (TListObject != null && TListObject.Count > 0)
                {
                    foreach (T TObject in TListObject)
                    {
                        var TObj = AddVersionProperty<T>(TObject);
                        TObj = AddTenentCodeProperty<T>(TObj);
                        TObj = AddProperty<T>(TObj, "ModifiedBy,DeletedBy");
                        TObj = AddDateTimeBasedOnProperty<T>(TObj, "ModifiedDateTime");
                        var entry = _dbContext.Entry(TObj);
                        _dbContext.Set<T>().Attach(TObj);
                        entry.State = System.Data.Entity.EntityState.Modified;
                        if (_dbContext.Entry(TObj).Entity.GetType().GetProperty("TNT_TenantCode") != null)
                            _dbContext.Entry(TObj).Property("TNT_TenantCode").IsModified = false;
                        else
                        {
                            _dbContext.Entry(TObj).Property("CMN_TenantCode").IsModified = false;
                        }
                        foreach (var item in RestrictPropertyName(TObj, "CreatedBy,CreatedDateTime"))
                        {
                            if (entry.Entity.GetType().GetProperty(item) != null)
                            {
                                entry.Property(item).IsModified = false;
                            }
                        }
                        if (!string.IsNullOrEmpty(LogQueueHelper.GetTableName(TObject)))
                        {

                            #region DES PreProcessing
                            //Starting the DES preprocessing which will check if there is any fields configured for events asynchronously and returns the changed fields
                            var desEntity = new DesEntity();
                            desEntity.Context = _dbContext;
                            //desEntity.transaction = trx;
                            desEntity.TenantCode = GetTenantCode();
                            desEntity.Entity = TObject;
                            desEntity.Action = "U";
                            var des = new RuleEngine();
                            des.desEntity = desEntity;
                            des.PreProcessing(TObject);
                            //Task<Dictionary<string, object>> desPreProcessingTask = Task.Factory.StartNew(()=> des.PreProcessing(TObject));
                            #endregion
                            #region Invoke Des Process
                            //*******DESV2************
                            //Wait for the DES Preprocessing to complete and get the changed fields. If there are any fields then call the DES Process
                            //desPreProcessingTask.Wait();
                            var desEvents = des.Execute();
                            if (desEvents != null && desEvents.Count > 0)
                                TrustHelper.SetObjectInRequestSession<DesEvent>("DesEvents", desEvents);
                            #endregion
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                throw;
            }
        }
        public virtual List<T> Update<T>(List<T> TListObject) where T : class
        {
            try
            {
                PrepareBulkUpdate(TListObject);
                using (DbContextTransaction transaction = _dbContext.Database.BeginTransaction())
                {

                    // LogQueue();
                    _dbContext.SaveChanges();
                    transaction.Commit();

                }
                if (!string.IsNullOrEmpty(LogQueueHelper.GetTableName(TListObject.FirstOrDefault())))
                {
                    PublishEvent();
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            finally { }
            return TListObject;
        }
        public virtual List<T> Update<T>(List<T> TListObject, DbContextTransaction transaction) where T : class
        {
            PrepareBulkUpdate(TListObject);
            using (transaction)
            {
                //*******DESV2************
                // LogQueue();
                _dbContext.SaveChanges();
            }
            return TListObject;
        }
        public virtual DbContext UpdateTransaction<T>(T TObject) where T : class
        {
            var TObj = AddVersionProperty<T>(TObject);
            TObj = AddTenentCodeProperty<T>(TObj);
            TObj = AddProperty<T>(TObj, "ModifiedBy,DeletedBy");
            TObj = AddDateTimeBasedOnProperty<T>(TObj, "ModifiedDateTime");
            var Val = (typeof(T).Name);
            var container = ((System.Data.Entity.Infrastructure.IObjectContextAdapter)_dbContext).ObjectContext.MetadataWorkspace.GetEntityContainer(((System.Data.Entity.Infrastructure.IObjectContextAdapter)_dbContext).ObjectContext.DefaultContainerName, System.Data.Entity.Core.Metadata.Edm.DataSpace.CSpace);
            string setName = (from meta in container.BaseEntitySets
                              where meta.ElementType.Name == Val
                              select meta.Name).First();

            UpdateContext.AttachToOrGet<T>(((System.Data.Entity.Infrastructure.IObjectContextAdapter)_dbContext).ObjectContext, setName, ref TObj);
            var entry = _dbContext.Entry(TObj);
            _dbContext.Set<T>().Attach(TObj);
            entry.State = System.Data.Entity.EntityState.Modified;
            if (entry.Entity.GetType().GetProperty("TNT_TenantCode") != null)
            {
                entry.Property("TNT_TenantCode").IsModified = false;
            }
            else if (entry.Entity.GetType().GetProperty("CMN_TenantCode") != null)
            {
                entry.Property("CMN_TenantCode").IsModified = false;
            }
            foreach (var item in RestrictPropertyName(TObj, "CreatedBy,CreatedDateTime"))
            {
                if (entry.Entity.GetType().GetProperty(item) != null)
                {
                    entry.Property(item).IsModified = false;
                }
            }
            if (!string.IsNullOrEmpty(LogQueueHelper.GetTableName(TObject)))
            {
                //*******DESV2************
                #region DES PreProcessing
                //Starting the DES preprocessing which will check if there is any fields configured for events asynchronously and returns the changed fields
                var desEntity = new DesEntity();
                desEntity.Context = _dbContext;
                //desEntity.transaction = trx;
                desEntity.TenantCode = GetTenantCode();
                desEntity.Entity = TObject;
                desEntity.Action = "U";
                var des = new RuleEngine();
                des.desEntity = desEntity;
                des.PreProcessing(TObject);
                //Task<Dictionary<string, object>> desPreProcessingTask = Task.Factory.StartNew(()=> des.PreProcessing(TObject));
                #endregion

                #region Invoke Des Process
                //*******DESV2************
                //Wait for the DES Preprocessing to complete and get the changed fields. If there are any fields then call the DES Process
                //desPreProcessingTask.Wait();
                var desEvents = des.Execute();
                if (desEvents != null && desEvents.Count > 0)
                    TrustHelper.SetObjectInRequestSession<DesEvent>("DesEvents", desEvents);
                #endregion
            }
            // LogQueue(_dbContext);
            return _dbContext;
        }

        public virtual T UpdateTransaction<T>(T TObject, DbContext _Ctx, DbContextTransaction trx) where T : class
        {
            string tableName = LogQueueHelper.GetTableName(TObject);
            var des = new RuleEngine();

            using (var _dbContext1 = SetTransaction(_Ctx, trx))
            {
                SharedEntity sharedEntity = new SharedEntity();
                _dbContext1.Database.UseTransaction(trx.UnderlyingTransaction);
                var TObj = AddVersionProperty<T>(TObject);
                TObj = AddTenentCodeProperty<T>(TObj);
                TObj = AddProperty<T>(TObj, "ModifiedBy,DeletedBy");
                TObj = AddDateTimeBasedOnProperty<T>(TObj, "ModifiedDateTime");
                var Val = (typeof(T).Name);
                var container = ((System.Data.Entity.Infrastructure.IObjectContextAdapter)_dbContext1).ObjectContext.MetadataWorkspace.GetEntityContainer(((System.Data.Entity.Infrastructure.IObjectContextAdapter)_dbContext1).ObjectContext.DefaultContainerName, System.Data.Entity.Core.Metadata.Edm.DataSpace.CSpace);
                string setName = (from meta in container.BaseEntitySets
                                  where meta.ElementType.Name == Val
                                  select meta.Name).First();

                UpdateContext.AttachToOrGet<T>(((System.Data.Entity.Infrastructure.IObjectContextAdapter)_dbContext1).ObjectContext, setName, ref TObj);
                var entry = _dbContext1.Entry(TObj);
                _dbContext1.Set<T>().Attach(TObj);
                entry.State = System.Data.Entity.EntityState.Modified;
                if (entry.Entity.GetType().GetProperty("TNT_TenantCode") != null)
                {
                    entry.Property("TNT_TenantCode").IsModified = false;
                }
                else if (entry.Entity.GetType().GetProperty("CMN_TenantCode") != null)
                {
                    entry.Property("CMN_TenantCode").IsModified = false;
                }
                foreach (var item in RestrictPropertyName(TObj, "CreatedBy,CreatedDateTime"))
                {
                    if (entry.Entity.GetType().GetProperty(item) != null)
                    {
                        entry.Property(item).IsModified = false;
                    }
                }
                sharedEntity.SaveJobSharedEntityDetails(TObject, _Ctx, trx, "U");

                if (!string.IsNullOrEmpty(tableName))
                {
                    #region DES PreProcessing
                    //Starting the DES preprocessing which will check if there is any fields configured for events asynchronously and returns the changed fields
                    var desEntity = new DesEntity();
                    desEntity.Context = _Ctx;
                    desEntity.transaction = trx;
                    desEntity.TenantCode = GetTenantCode();
                    desEntity.Entity = TObject;
                    desEntity.Action = "U";
                    des.desEntity = desEntity;
                    des.PreProcessing(TObject);
                    //Task<Dictionary<string, object>> desPreProcessingTask = Task.Factory.StartNew(()=> des.PreProcessing(TObject));
                    #endregion

                    #region Invoke Des Process
                    //*******DESV2************
                    //Wait for the DES Preprocessing to complete and get the changed fields. If there are any fields then call the DES Process
                    //desPreProcessingTask.Wait();
                    var desEvents = des.Execute();
                    if (desEvents != null && desEvents.Count > 0)
                        TrustHelper.SetObjectInRequestSession<DesEvent>("DesEvents_Transaction", desEvents);
                    #endregion
                }
                // LogQueue(_dbContext1, trx);
                _dbContext1.SaveChanges();



                return TObject;
            }
        }
        public virtual T Update<T>(T TObject, DbContext _Ctx, DbContextTransaction transaction) where T : class
        {
            Int32 sharedEntityCount = 0;
            SharedEntity objSharedEntity = new SharedEntity();
            List<DataSharedEntity> listSharedEntity = new List<DataSharedEntity>();
            if (TObject != null)
            {

                SharedEntity sharedEntity = new SharedEntity();
                var TObj = AddVersionProperty<T>(TObject);
                TObj = AddTenentCodeProperty<T>(TObj);
                TObj = AddProperty<T>(TObj, "ModifiedBy,DeletedBy");
                TObj = AddDateTimeBasedOnProperty<T>(TObj, "ModifiedDateTime");
                var Val = (typeof(T).Name);
                var container = ((System.Data.Entity.Infrastructure.IObjectContextAdapter)_Ctx).ObjectContext.MetadataWorkspace.GetEntityContainer(((System.Data.Entity.Infrastructure.IObjectContextAdapter)_Ctx).ObjectContext.DefaultContainerName, System.Data.Entity.Core.Metadata.Edm.DataSpace.CSpace);
                string setName = (from meta in container.BaseEntitySets
                                  where meta.ElementType.Name == Val
                                  select meta.Name).First();

                UpdateContext.AttachToOrGet<T>(((System.Data.Entity.Infrastructure.IObjectContextAdapter)_Ctx).ObjectContext, setName, ref TObj);
                var entry = _Ctx.Entry(TObj);
                _Ctx.Set<T>().Attach(TObj);
                entry.State = System.Data.Entity.EntityState.Modified;
                if (entry.Entity.GetType().GetProperty("TNT_TenantCode") != null)
                {
                    entry.Property("TNT_TenantCode").IsModified = false;
                }
                else if (entry.Entity.GetType().GetProperty("CMN_TenantCode") != null)
                {
                    entry.Property("CMN_TenantCode").IsModified = false;
                }
                foreach (var item in RestrictPropertyName(TObj, "CreatedBy,CreatedDateTime"))
                {
                    if (entry.Entity.GetType().GetProperty(item) != null)
                    {
                        entry.Property(item).IsModified = false;
                    }
                }
            }
            sharedEntityCount = CheckSharedEntityCount(TObject);
            if (sharedEntityCount > 0)
                listSharedEntity = objSharedEntity.PrepareJobSharedEntityDetails(TObject, _Ctx, transaction, "U");
            if (!string.IsNullOrEmpty(LogQueueHelper.GetTableName(TObject)))
            {
                //*******DESV2************

                #region DES PreProcessing
                //Starting the DES preprocessing which will check if there is any fields configured for events asynchronously and returns the changed fields
                var desEntity = new DesEntity();
                desEntity.Context = _Ctx;
                desEntity.transaction = transaction;
                desEntity.TenantCode = GetTenantCode();
                desEntity.Entity = TObject;
                desEntity.Action = "U";
                var des = new RuleEngine();
                des.desEntity = desEntity;
                des.PreProcessing(TObject);
                //Task<Dictionary<string, object>> desPreProcessingTask = Task.Factory.StartNew(()=> des.PreProcessing(TObject));
                #endregion



                #region Invoke Des Process
                //*******DESV2************
                //Wait for the DES Preprocessing to complete and get the changed fields. If there are any fields then call the DES Process
                //desPreProcessingTask.Wait();
                var desEvents = des.Execute();
                if (desEvents != null && desEvents.Count > 0)
                    TrustHelper.SetObjectInRequestSession<DesEvent>("DesEvents", desEvents);
                #endregion
            }
            // LogQueue(_Ctx, transaction);
            _Ctx.SaveChanges();
            if (!string.IsNullOrEmpty(LogQueueHelper.GetTableName(TObject)))
            {
                PublishEvent();
            }
            return TObject;
        }

        public virtual List<T> UpsertList<T>(List<T> TObject, DbContext _Ctx, DbContextTransaction trx) where T : class
        {
            if (TObject != null)
            {
                foreach (var tobj in TObject)
                {
                    AddVersionProperty<T>(tobj);
                    if (CheckTenantValid<T>(tobj))
                        AddTenentCodeProperty<T>(tobj);
                    AddProperty<T>(tobj, "CreatedBy,UploadedBy,ModifiedBy");
                    AddDateTimeBasedOnProperty<T>(tobj, "CreatedDateTime,ModifiedDateTime");
                }
                //_Ctx.Database.UseTransaction(trx.UnderlyingTransaction);
                _Ctx.Upsert(TObject).Execute();
            }

            _Ctx.SaveChanges();
            return TObject;
        }

        public virtual T UpdateVersion<T>(T TObject) where T : class
        {
            try
            {
                var TObj = AddVersionProperty<T>(TObject);
                var entry = _dbContext.Entry(TObj);
                _dbContext.Set<T>().Attach(TObj);
                entry.State = System.Data.Entity.EntityState.Modified;
                _dbContext.SaveChanges();
                return TObj;
            }
            catch (Exception)
            {

                throw;
            }

        }
        public virtual T SelectedUpdate<T>(T Entity, ColumnUpdate ColumnUpdate, bool IsDesNeeded = true) where T : class
        {
            _dbContext.Configuration.ValidateOnSaveEnabled = false;
            T dEntity;
            try
            {

                dEntity = SelectedUpdateEntityCheck(Entity, ColumnUpdate);

                if (dEntity != null)
                {
                    Entity = SetValueToProperty<T>(dEntity, ColumnUpdate?.Properties);
                    dEntity = AddProperty<T>(dEntity, "ModifiedBy");//Newly added to capture the Modified By
                    dEntity = AddDateTimeBasedOnProperty<T>(dEntity, "ModifiedDateTime"); //Newly added to capture the Modified DateTime
                    var entry = _dbContext.Entry(dEntity);
                    _dbContext.Set<T>().Attach(dEntity);
                    foreach (var Property in ColumnUpdate?.Properties)
                    {
                        entry.Property(Property.PropertyName).IsModified = true;
                    }
                    // Also set IsModified = true for properties ending with "ModifiedBy" or "ModifiedDateTime"
                    foreach (var propertyName in typeof(T).GetProperties().Where(p => p.Name.EndsWith("ModifiedBy") || p.Name.EndsWith("ModifiedDateTime")).Select(p => p.Name))
                    {
                        if (entry.Property(propertyName) != null && entry.Property(propertyName).IsModified == false)
                        {
                            entry.Property(propertyName).IsModified = true;
                        }
                    }
                    if (!string.IsNullOrEmpty(LogQueueHelper.GetTableName(dEntity)) && IsDesNeeded)
                    {
                        //*******DESV2************

                        #region DES PreProcessing
                        //Starting the DES preprocessing which will check if there is any fields configured for events asynchronously and returns the changed fields
                        var desEntity = new DesEntity();
                        desEntity.Context = _dbContext;
                        //desEntity.transaction = trx;
                        desEntity.TenantCode = GetTenantCode();
                        desEntity.Entity = dEntity;
                        desEntity.Action = "U";
                        var des = new RuleEngine();
                        des.desEntity = desEntity;
                        des.PreProcessing(dEntity);
                        //Task<Dictionary<string, object>> desPreProcessingTask = Task.Factory.StartNew(()=> des.PreProcessing(TObject));
                        #endregion

                        #region Invoke Des Process
                        //*******DESV2************
                        //Wait for the DES Preprocessing to complete and get the changed fields. If there are any fields then call the DES Process
                        //desPreProcessingTask.Wait();
                        var desEvents = des.Execute();
                        if (desEvents != null && desEvents.Count > 0)
                            TrustHelper.SetObjectInRequestSession<DesEvent>("DesEvents", desEvents);
                        #endregion
                    }
                    // LogQueue(_dbContext);
                    _dbContext.SaveChanges();
                    if (!string.IsNullOrEmpty(LogQueueHelper.GetTableName(Entity)) && IsDesNeeded)
                    {
                        PublishEvent();
                    }
                    return Entity;
                }
                else
                {
                    return dEntity;
                }
            }
            finally
            {
                _dbContext.Configuration.ValidateOnSaveEnabled = true;
            }

        }
        public virtual T SelectedUpdateTransaction<T>(T Entity, ColumnUpdate ColumnUpdate, DbContext _Ctx, DbContextTransaction trx, bool IssharedEntityrequired = false, bool IsDesNeeded = true) where T : class
        {
            T dEntity;
            try
            {
                dEntity = SelectedUpdateEntityCheck(Entity, ColumnUpdate);
                if (dEntity != null)
                {
                    using (var _dbContext1 = SetTransaction(_Ctx, trx))
                    {
                        _Ctx.Configuration.ValidateOnSaveEnabled = false;
                        _dbContext1.Database.UseTransaction(trx.UnderlyingTransaction);
                        Entity = SetValueToProperty<T>(dEntity, ColumnUpdate?.Properties);

                        Entity = AddProperty<T>(Entity, "ModifiedBy");//Newly added to capture the Modified By
                        Entity = AddDateTimeBasedOnProperty<T>(Entity, "ModifiedDateTime"); //Newly added to capture the Modified DateTime

                        var entry = _dbContext1.Entry(Entity);
                        _dbContext1.Set<T>().Attach(Entity);
                        foreach (var Property in ColumnUpdate?.Properties)
                        {
                            entry.Property(Property.PropertyName).IsModified = true;
                        }
                        // Also set IsModified = true for properties ending with "ModifiedBy" or "ModifiedDateTime"
                        foreach (var propertyName in typeof(T).GetProperties().Where(p => p.Name.EndsWith("ModifiedBy") || p.Name.EndsWith("ModifiedDateTime")).Select(p => p.Name))
                        {
                            if (entry.Property(propertyName) != null && entry.Property(propertyName).IsModified == false)
                            {
                                entry.Property(propertyName).IsModified = true;
                            }
                        }
                        if (IssharedEntityrequired)
                        {
                            SharedEntity sharedEntity = new SharedEntity();
                            sharedEntity.SaveJobSharedEntityDetails(Entity, _Ctx, trx, "U");
                        }
                        if (!string.IsNullOrEmpty(LogQueueHelper.GetTableName(Entity)) && IsDesNeeded)
                        {
                            //*******DESV2************

                            #region DES PreProcessing
                            //Starting the DES preprocessing which will check if there is any fields configured for events asynchronously and returns the changed fields
                            var desEntity = new DesEntity();
                            desEntity.Context = _Ctx;
                            desEntity.transaction = trx;
                            desEntity.TenantCode = GetTenantCode();
                            desEntity.Entity = dEntity;
                            desEntity.Action = "U";
                            var des = new RuleEngine();
                            des.desEntity = desEntity;
                            des.PreProcessing(dEntity);
                            //Task<Dictionary<string, object>> desPreProcessingTask = Task.Factory.StartNew(()=> des.PreProcessing(TObject));
                            #endregion

                            // LogQueue(_dbContext1, trx);
                            _dbContext1.SaveChanges();

                            #region Invoke Des Process
                            //*******DESV2************
                            //Wait for the DES Preprocessing to complete and get the changed fields. If there are any fields then call the DES Process
                            //desPreProcessingTask.Wait();
                            var desEvents = des.Execute();
                            if (desEvents != null && desEvents.Count > 0)
                                TrustHelper.SetObjectInRequestSession<DesEvent>("DesEvents_Transaction", desEvents);
                            #endregion
                        }
                        // LogQueue(_dbContext1, trx);
                        _dbContext1.SaveChanges();
                        _Ctx.Configuration.ValidateOnSaveEnabled = true;
                        return Entity;
                    }
                }
                else
                {
                    return dEntity;
                }
            }
            catch (Exception Ex)
            {
                throw Ex;
            }
            finally
            {

            }

        }
        public virtual List<T> UpdateListTransaction<T>(List<T> TListObject, DbContext _Ctx, DbContextTransaction transaction) where T : class
        {
            Int32 sharedEntityCount = 0;
            SharedEntity objSharedEntity = new SharedEntity();
            List<DataSharedEntity> listSharedEntity = new List<DataSharedEntity>();
            string tableName = string.Empty;
            if (TListObject != null && TListObject.Count > 0)
            {
                var firstObj = TListObject.FirstOrDefault();
                if (firstObj != null)
                    tableName = LogQueueHelper.GetTableName(firstObj);

                foreach (T TObject in TListObject)
                {
                    if (TObject != null)
                    {
                        SharedEntity sharedEntity = new SharedEntity();
                        //_dbContext1.Database.UseTransaction(trx.UnderlyingTransaction);
                        var TObj = AddVersionProperty<T>(TObject);
                        TObj = AddTenentCodeProperty<T>(TObj);
                        TObj = AddProperty<T>(TObj, "ModifiedBy,DeletedBy");
                        TObj = AddDateTimeBasedOnProperty<T>(TObj, "ModifiedDateTime");
                        var Val = (typeof(T).Name);
                        var container = ((System.Data.Entity.Infrastructure.IObjectContextAdapter)_Ctx).ObjectContext.MetadataWorkspace.GetEntityContainer(((System.Data.Entity.Infrastructure.IObjectContextAdapter)_Ctx).ObjectContext.DefaultContainerName, System.Data.Entity.Core.Metadata.Edm.DataSpace.CSpace);
                        string setName = (from meta in container.BaseEntitySets
                                          where meta.ElementType.Name == Val
                                          select meta.Name).First();

                        UpdateContext.AttachToOrGet<T>(((System.Data.Entity.Infrastructure.IObjectContextAdapter)_Ctx).ObjectContext, setName, ref TObj);
                        var entry = _Ctx.Entry(TObj);
                        _Ctx.Set<T>().Attach(TObj);
                        entry.State = System.Data.Entity.EntityState.Modified;
                    }
                    sharedEntityCount = CheckSharedEntityCount(TObject);
                    if (sharedEntityCount > 0)
                        listSharedEntity = objSharedEntity.PrepareJobSharedEntityDetails(TObject, _Ctx, transaction, "U");
                }
            }
            //string tableName = LogQueueHelper.GetTableName(TListObject?.FirstOrDefault());
            if (!string.IsNullOrEmpty(tableName))
            {
                TListObject.ForEach(ent =>
                {
                    //*******DESV2************

                    #region DES PreProcessing
                    //Starting the DES preprocessing which will check if there is any fields configured for events asynchronously and returns the changed fields
                    var desEntity = new DesEntity();
                    desEntity.Context = _Ctx;
                    desEntity.transaction = transaction;
                    desEntity.TenantCode = GetTenantCode();
                    desEntity.Entity = ent;
                    desEntity.Action = "U";
                    var des = new RuleEngine();
                    des.desEntity = desEntity;
                    des.PreProcessing(ent, TListObject, tableName);
                    //Task<Dictionary<string, object>> desPreProcessingTask = Task.Factory.StartNew(()=> des.PreProcessing(TObject));
                    #endregion



                    #region Invoke Des Process
                    //*******DESV2************
                    //Wait for the DES Preprocessing to complete and get the changed fields. If there are any fields then call the DES Process
                    //desPreProcessingTask.Wait();
                    var desEvents = des.Execute();
                    if (desEvents != null && desEvents.Count > 0)
                        TrustHelper.SetObjectInRequestSession<DesEvent>("DesEvents_Transaction", desEvents);
                    #endregion
                });
            }
            // LogQueue(_Ctx, transaction);
            _Ctx.SaveChanges();
            return TListObject;
        }
        public virtual List<T> ListSelectedUpdate<T>(List<T> Entity, List<ColumnUpdate> lstColumnUpdate, bool IsDesNeeded = true) where T : class
        {
            string key = string.Empty;
            try
            {
                List<T> LstEntity = new List<T>();
                T dynEntityObj = Entity.FirstOrDefault();
                foreach (var ColumnUpdate in lstColumnUpdate)
                {
                    var keyValue = LogQueueHelper.GetPrimaryKey<T>(dynEntityObj);
                    string pkey = keyValue["Key"];
                    LstEntity.Add(SelectedUpdate<T>(Entity.Where(x => x.GetType().GetProperty(pkey).GetValue(x)?.ToString() == ColumnUpdate.EntityRefPK).FirstOrDefault(), ColumnUpdate, IsDesNeeded: IsDesNeeded));

                }
                return Entity;
            }
            catch
            {
                throw;
            }
        }

        #endregion

        #region GetById Entity Based

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

        #region GetMany Entity Based
        public virtual dynamic GetManyCount<T>(Expression<Func<T, bool>> predicate, FilterInput lstFilterInput = null) where T : class
        {
            // Check the header value
            var request = HttpContext.Current?.Request;
            string text = request?.Headers?.Get("Value");
            string countHeaderValue = request?.Headers?.Get("Iscountneeded");

            if (!string.IsNullOrEmpty(text) && string.Equals(text, "needquery", StringComparison.OrdinalIgnoreCase))
            {
                return GetQuery(predicate);
            }

            // If countRequired is false, return null or some other default value
            if ((lstFilterInput != null && lstFilterInput.IsCountRequired) ||
                (!string.IsNullOrEmpty(countHeaderValue) && string.Equals(countHeaderValue, "true", StringComparison.OrdinalIgnoreCase)))
            {
                // If no predicate is provided, return the total count of the set
                if (predicate == null)
                {
                    return _dbContext.Set<T>().Count();
                }

                // Otherwise, return the count of entities matching the predicate
                return _dbContext.Set<T>().Count(predicate);
            }
            else
            {
                return 1;
            }
        }

        public virtual IQueryable<T> GetMany<T>(Expression<Func<T, bool>> predicate, FilterInput lstFilterInput = null) where T : class
        {
            if (predicate == null)
            {
                var visitor = new DistinctColumnVisitor();
                visitor.LogPredicate("null");
                if (IsRestrictFetchingAllRecords) return _dbContext.Set<T>().AsQueryable<T>().Take(MaxRowsToFetchIfPredicateNull);
               return _dbContext.Set<T>().AsQueryable<T>();
            }
            else if (lstFilterInput != null && lstFilterInput.IsCacheRequired && !string.IsNullOrEmpty(lstFilterInput.CacheKey))
            {
                return GetDataFromCache<T>(predicate, lstFilterInput);
            }
            else
            {
                var visitor = new DistinctColumnVisitor();
                visitor.Visit(predicate);
                bool IsOnlyTargetMemberPresent =  visitor.DistinctColumns.IsSubsetOf(SystemFilterCodes);
                if (IsOnlyTargetMemberPresent)
                {
                    visitor.LogPredicate(predicate.ToString());
                    if (IsRestrictFetchingAllRecords) return _dbContext.Set<T>().Where<T>(predicate).AsQueryable<T>().AsNoTracking().Take(MaxRowsToFetchIfPredicateNull);
                }
                return _dbContext.Set<T>().Where<T>(predicate).AsQueryable<T>().AsNoTracking();// Commented by Gobi
            }
        }
        public virtual List<T> GetMany<T>(Expression<Func<T, bool>> predicate, DbContext _Ctx, DbContextTransaction trx) where T : class
        {
            List<T> _data = new List<T>();

            if (predicate == null)
                _data = _Ctx.Set<T>().AsQueryable<T>().ToList();
            else
                _data = _Ctx.Set<T>().Where<T>(predicate).AsQueryable<T>().AsNoTracking().ToList();// Commented by Gobi
            return _data;


        }
        public virtual T GetbyColumn<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            if (predicate == null)
                return _dbContext.Set<T>().FirstOrDefault();
            else
                return _dbContext.Set<T>().FirstOrDefault<T>(predicate);

        }
        #region Multiple column sorting
        public virtual IQueryable<T> GetManyWithOrderByExpression<T>(Expression<Func<T, bool>> predicate, int index, int size, string strSortColumn, string strSortType, List<PageSorted> lstPageSorteds) where T : class
        {
            IQueryable<T> Result = null;
            if (predicate == null)
                Result = _dbContext.Set<T>().Where<T>(predicate).OrderByWithExpression(lstPageSorteds).Skip((index - 1) * size).Take(size).AsQueryable<T>();
            else
                Result = _dbContext.Set<T>().Where<T>(predicate).OrderByWithExpression(lstPageSorteds).Skip((index - 1) * size).Take(size).AsQueryable<T>();
            return Result;
        }
        #endregion
        public virtual IQueryable<T> GetMany<T>(Expression<Func<T, bool>> predicate, int index, int size, string strSortColumn, string strSortType) where T : class
        {
            IQueryable<T> Result = null;
            Type Type = typeof(T);
            Type propertyType = default(Type);
            string propertyname = strSortColumn.Split(',').ToList().FirstOrDefault();
            PropertyInfo PropertyInfo = Type.GetProperty(propertyname);
            propertyType = PropertyInfo.PropertyType;
            TypeCode typeCode = Type.GetTypeCode(PropertyInfo.PropertyType.GetTypeInfo());
            strSortColumn = "new(" + strSortColumn + ")";

            var Objectparam = Expression.Parameter(typeof(T), "op");
            Expression Objectparent = Expression.Property(Objectparam, propertyname);

            bool isNull = false;
            if (propertyType.IsGenericType &&
                    propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                isNull = true;
                propertyType = propertyType.GetGenericArguments()[0];
            }
            typeCode = Type.GetTypeCode(propertyType.GetTypeInfo());

            switch (typeCode)
            {
                case TypeCode.DateTime:
                    #region DateTimeObject
                    if (!isNull)
                    {
                        var DateTimesortExpression = Expression.Lambda<Func<T, DateTime>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                Result = _dbContext.Set<T>().OrderByDesc(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                            else
                                Result = _dbContext.Set<T>().OrderBy(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                Result = _dbContext.Set<T>().Where<T>(predicate).OrderByDesc(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                            else
                                Result = _dbContext.Set<T>().Where<T>(predicate).OrderBy(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                        }
                    }
                    else
                    {
                        var DateTimenullsortExpression = Expression.Lambda<Func<T, DateTime?>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                Result = _dbContext.Set<T>().OrderByDesc(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                            else
                                Result = _dbContext.Set<T>().OrderBy(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                Result = _dbContext.Set<T>().Where<T>(predicate).OrderByDesc(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                            else
                                Result = _dbContext.Set<T>().Where<T>(predicate).OrderBy(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                        }
                    }
                    break;
                #endregion
                case TypeCode.Decimal:
                    #region DecimalObject
                    if (!isNull)
                    {
                        var DecimalsortExpression = Expression.Lambda<Func<T, Decimal>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                Result = _dbContext.Set<T>().OrderByDesc(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                            else
                                Result = _dbContext.Set<T>().OrderBy(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                Result = _dbContext.Set<T>().Where<T>(predicate).OrderByDesc(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                            else
                                Result = _dbContext.Set<T>().Where<T>(predicate).OrderBy(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                        }
                    }
                    else
                    {
                        var DecimalNullsortExpression = Expression.Lambda<Func<T, Decimal?>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                Result = _dbContext.Set<T>().OrderByDesc(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                            else
                                Result = _dbContext.Set<T>().OrderBy(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                Result = _dbContext.Set<T>().Where<T>(predicate).OrderByDesc(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                            else
                                Result = _dbContext.Set<T>().Where<T>(predicate).OrderBy(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                        }

                    }
                    break;
                #endregion
                case TypeCode.Int64:
                    #region Int64Object
                    if (!isNull)
                    {
                        var Int64sortExpression = Expression.Lambda<Func<T, Int64>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                Result = _dbContext.Set<T>().OrderByDesc(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                            else
                                Result = _dbContext.Set<T>().OrderBy(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                Result = _dbContext.Set<T>().Where<T>(predicate).OrderByDesc(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                            else
                                Result = _dbContext.Set<T>().Where<T>(predicate).OrderBy(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                        }
                    }
                    else
                    {
                        var Int64NullsortExpression = Expression.Lambda<Func<T, Int64?>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                Result = _dbContext.Set<T>().OrderByDesc(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                            else
                                Result = _dbContext.Set<T>().OrderBy(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                Result = _dbContext.Set<T>().Where<T>(predicate).OrderByDesc(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                            else
                                Result = _dbContext.Set<T>().Where<T>(predicate).OrderBy(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                        }
                    }
                    break;
                #endregion
                case TypeCode.Int32:
                    #region Int32Object
                    if (!isNull)
                    {
                        var Int32sortExpression = Expression.Lambda<Func<T, Int32>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                Result = _dbContext.Set<T>().OrderByDesc(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                            else
                                Result = _dbContext.Set<T>().OrderBy(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                Result = _dbContext.Set<T>().Where<T>(predicate).OrderByDesc(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                            else
                                Result = _dbContext.Set<T>().Where<T>(predicate).OrderBy(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                        }
                    }
                    else
                    {
                        var Int32NullsortExpression = Expression.Lambda<Func<T, Int32?>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                Result = _dbContext.Set<T>().OrderByDesc(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                            else
                                Result = _dbContext.Set<T>().OrderBy(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                Result = _dbContext.Set<T>().Where<T>(predicate).OrderByDesc(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                            else
                                Result = _dbContext.Set<T>().Where<T>(predicate).OrderBy(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                        }
                    }
                    break;
                #endregion
                case TypeCode.String:
                    #region StringObject
                    var stringSortExpression = Expression.Lambda<Func<T, string>>(Objectparent, Objectparam);
                    if (predicate == null)
                    {
                        if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                            Result = _dbContext.Set<T>().OrderByDesc(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                        else if (strSortType.ToLower() == "pdesc")
                            Result = _dbContext.Set<T>().OrderByDesc(strSortColumn).AsQueryable<T>();
                        else if (strSortType.ToLower() == "pasc")
                            Result = _dbContext.Set<T>().OrderBy(strSortColumn).AsQueryable<T>();
                        else
                            Result = _dbContext.Set<T>().OrderBy(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(strSortType))
                        {

                            if (strSortType.ToLower() == "desc")
                                Result = _dbContext.Set<T>().Where<T>(predicate).OrderByDesc(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                            else if (strSortType.ToLower() == "pdesc")
                                Result = _dbContext.Set<T>().Where<T>(predicate).OrderByDesc(strSortColumn).AsQueryable<T>();
                            else if (strSortType.ToLower() == "pasc")
                                Result = _dbContext.Set<T>().Where<T>(predicate).OrderBy(strSortColumn).AsQueryable<T>();
                            else
                                Result = _dbContext.Set<T>().Where<T>(predicate).OrderBy(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                        }
                    }
                    break;
                #endregion
                default:
                    #region Object

                    var objectsortExpression = Expression.Lambda<Func<T, object>>(Objectparent, Objectparam);
                    if (predicate == null)
                    {
                        if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                            Result = _dbContext.Set<T>().OrderByDesc(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                        else
                            Result = _dbContext.Set<T>().OrderBy(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                            Result = _dbContext.Set<T>().Where<T>(predicate).OrderByDesc(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                        else
                            Result = _dbContext.Set<T>().Where<T>(predicate).OrderBy(strSortColumn).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                    }
                    break;
                    #endregion
            }
            return Result;
        }
        public virtual DbOutput<T> GetMany<T>(Expression<Func<T, bool>> predicate, DbInput dbInput) where T : class
        {
            DbOutput<T> output = new DbOutput<T>();
            IQueryable<T> Result = null;

            Type Type = typeof(T);
            var selectCols = dbInput.SelectList != null ?
                string.Join(",", dbInput.SelectList) :
                string.Join(",", Type.GetProperties().Select(p => p.Name).ToArray());

            var sortColumn = string.IsNullOrEmpty(dbInput.SortBy) ? string.Empty : "new(" + dbInput.SortBy + ")";
            var selectedColumns = "new(" + selectCols + ")";

            Result = _dbContext.Set<T>();
            Result = predicate == null ? Result : Result.Where<T>(predicate);
            Result = (dbInput?.SortType?.ToLower() == "desc") ? Result.OrderByDesc(sortColumn) :
                        (dbInput?.SortType?.ToLower() == "asc" || !string.IsNullOrEmpty(dbInput.SortType)) ? Result.OrderBy(sortColumn)
            : Result;
            Result = (dbInput?.Index > 0 && dbInput?.Size > 0) ? Result.Skip((dbInput.Index - 1) * dbInput.Size).Take(dbInput.Size).AsQueryable<T>() : Result.AsQueryable<T>();

            output.ResultQuery = Result.Select(a => a);
            output.RecordCount = output.ResultQuery.Count();
            output.Status = DbResultStatus.Success;
            return output;
        }
        public virtual IQueryable<T> GetManyv1<T>(Expression<Func<T, bool>> predicate, int index, int size, string strSortColumn, string strSortType) where T : class
        {
            //var test = decimal;
            var param = Expression.Parameter(typeof(T), "p");
            Expression parent = Expression.Property(param, strSortColumn);
            var sortExpression = Expression.Lambda<Func<T, object>>(parent, param);

            if (predicate == null)
                if (strSortType == "desc")
                    return _dbContext.Set<T>().OrderByDescending(sortExpression).Skip((index - 1) * size).Take(size).AsQueryable<T>();
                else
                    return _dbContext.Set<T>().OrderBy(sortExpression).Skip((index - 1) * size).Take(size).AsQueryable<T>();
            else
                if (strSortType == "desc")
                return _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(sortExpression).Skip((index - 1) * size).Take(size).AsQueryable<T>();
            else
                return _dbContext.Set<T>().Where<T>(predicate).OrderBy(sortExpression).ThenBy(sortExpression).Skip((index - 1) * size).Take(size).AsQueryable<T>();

        }
        public List<Dictionary<string, object>> GetManyWithSelectedColumns<T>(Expression<Func<T, bool>> predicate, string SelectedColumns, string UtcToLocalProperty = "") where T : class
        {
            dynamic dynamicList;
            List<Dictionary<string, object>> keyValuePairsDto = new List<Dictionary<string, object>>();

            SelectedColumns = "new(" + SelectedColumns + ")";
            if (predicate == null)
                dynamicList = _dbContext.Set<T>().AsQueryable().Select(SelectedColumns);
            else
                dynamicList = _dbContext.Set<T>().Where<T>(predicate).AsQueryable().Select(SelectedColumns);

            keyValuePairsDto = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(JsonConvert.SerializeObject(dynamicList));

            return convertUTCtoLocalDateTimeDictinory<T>(keyValuePairsDto, UtcToLocalProperty).Select(x => { x = x.ToDictionary(xd => xd.Key.Substring(xd.Key.IndexOf('_') + 1), xd => xd.Value); return x; }).ToList();//Remove Prefix from Key;
        }
        public List<Dictionary<string, object>> GetManyWithSelectedColumns<T>(Expression<Func<T, bool>> predicate, string SelectedColumns, DbContext _Ctx, DbContextTransaction trx, string UtcToLocalProperty = "") where T : class
        {
            dynamic dynamicList;
            List<Dictionary<string, object>> keyValuePairsDto = new List<Dictionary<string, object>>();

            SelectedColumns = "new(" + SelectedColumns + ")";

            using (DbContext _DC = SetTransaction(_Ctx, trx))
            {
                _DC.Database.UseTransaction(trx.UnderlyingTransaction);
                if (predicate == null)
                    dynamicList = _DC.Set<T>().AsQueryable().Select(SelectedColumns);
                else
                    dynamicList = _DC.Set<T>().Where<T>(predicate).AsQueryable().Select(SelectedColumns);

                keyValuePairsDto = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(JsonConvert.SerializeObject(dynamicList));

                return convertUTCtoLocalDateTimeDictinory<T>(keyValuePairsDto, UtcToLocalProperty).Select(x => { x = x.ToDictionary(xd => xd.Key.Substring(xd.Key.IndexOf('_') + 1), xd => xd.Value); return x; }).ToList();//Remove Prefix from Key;
            }

        }
        public List<Dictionary<string, object>> GetManyWithSelectedColumns<T>(Expression<Func<T, bool>> predicate, string SelectedColumns, int index, int size, string strSortColumn, string strSortType, string UtcToLocalProperty = "") where T : class
        {
            dynamic dynamicList = null;
            List<Dictionary<string, object>> keyValuePairsDto = new List<Dictionary<string, object>>();

            Type Type = typeof(T);
            Type propertyType = default(Type);
            PropertyInfo PropertyInfo;
            if (string.IsNullOrEmpty(strSortColumn))
            {
                //var sortcolumn = Type.GetProperties().ToList().Select(x => x.Name).FirstOrDefault();
                //var sortcolumn = Type.GetProperties().Where(x => x.PropertyType.FullName != "System.Guid").ToList().Select(x => x.Name).FirstOrDefault();
                //var sortcolumn = Type.GetProperties().Where(x => x.Name.Contains("CreatedDateTime")).Select(x => x.Name).ToString();
                //var sortcolumn = Type.GetProperties().Where(x => x.PropertyType.FullName != "System.Guid" && !(x.PropertyType.IsGenericType
                //&& x.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>) && x.PropertyType.GetGenericArguments()[0].Name == "Guid")).FirstOrDefault();

                //var sortcolumn = Type.GetProperties().Where(x => x.PropertyType.FullName != "System.Guid" && !(x.PropertyType.IsGenericType
                //&& x.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>)
                //&& x.PropertyType.GetGenericArguments()[0].Name == "Guid")).ToList().Select(x => x.Name).FirstOrDefault();

                var sortcolumn = Type.GetProperties().Where(x => x.PropertyType.FullName != "System.Guid" && x.PropertyType.FullName != "System.Boolean" && !(x.PropertyType.IsGenericType
                && x.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>)
                && x.PropertyType.GetGenericArguments()[0].Name == "Guid")).ToList().Select(x => x.Name).FirstOrDefault();

                PropertyInfo = Type.GetProperty(sortcolumn);
                strSortColumn = sortcolumn;
                strSortType = "Asc";
            }
            else
                PropertyInfo = Type.GetProperty(strSortColumn);
            propertyType = PropertyInfo.PropertyType;
            TypeCode typeCode = Type.GetTypeCode(PropertyInfo.PropertyType.GetTypeInfo());
            SelectedColumns = "new(" + SelectedColumns + ")";

            var Objectparam = Expression.Parameter(typeof(T), "op");
            Expression Objectparent = Expression.Property(Objectparam, strSortColumn);

            bool isNull = false;
            if (propertyType.IsGenericType &&
                    propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                isNull = true;
                propertyType = propertyType.GetGenericArguments()[0];
            }
            typeCode = Type.GetTypeCode(propertyType.GetTypeInfo());


            switch (typeCode)
            {
                case TypeCode.DateTime:
                    #region DateTimeObject
                    if (!isNull)
                    {
                        var DateTimesortExpression = Expression.Lambda<Func<T, DateTime>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = _dbContext.Set<T>().OrderByDescending(DateTimesortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                            else
                                dynamicList = _dbContext.Set<T>().OrderBy(DateTimesortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(DateTimesortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                            else
                                dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderBy(DateTimesortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                        }
                    }
                    else
                    {
                        var DateTimenullsortExpression = Expression.Lambda<Func<T, DateTime?>>(Objectparent, Objectparam);

                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = _dbContext.Set<T>().OrderByDescending(DateTimenullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                            else
                                dynamicList = _dbContext.Set<T>().OrderBy(DateTimenullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(DateTimenullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                            else
                                dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderBy(DateTimenullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                        }
                    }
                    break;
                #endregion
                case TypeCode.Decimal:
                    #region DecimalObject
                    if (!isNull)
                    {
                        var DecimalsortExpression = Expression.Lambda<Func<T, Decimal>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = _dbContext.Set<T>().OrderByDescending(DecimalsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                            else
                                dynamicList = _dbContext.Set<T>().OrderBy(DecimalsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(DecimalsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                            else
                                dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderBy(DecimalsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                        }
                    }
                    else
                    {
                        var DecimalNullsortExpression = Expression.Lambda<Func<T, Decimal?>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = _dbContext.Set<T>().OrderByDescending(DecimalNullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                            else
                                dynamicList = _dbContext.Set<T>().OrderBy(DecimalNullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(DecimalNullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                            else
                                dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderBy(DecimalNullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                        }

                    }
                    break;
                #endregion
                case TypeCode.Int64:
                    #region Int64Object
                    if (!isNull)
                    {
                        var Int64sortExpression = Expression.Lambda<Func<T, Int64>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = _dbContext.Set<T>().OrderByDescending(Int64sortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                            else
                                dynamicList = _dbContext.Set<T>().OrderBy(Int64sortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(Int64sortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                            else
                                dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderBy(Int64sortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                        }
                    }
                    else
                    {
                        var Int64NullsortExpression = Expression.Lambda<Func<T, Int64?>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = _dbContext.Set<T>().OrderByDescending(Int64NullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                            else
                                dynamicList = _dbContext.Set<T>().OrderBy(Int64NullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(Int64NullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                            else
                                dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderBy(Int64NullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                        }
                    }
                    break;
                #endregion
                case TypeCode.Int32:
                    #region Int32Object
                    if (!isNull)
                    {
                        var Int32sortExpression = Expression.Lambda<Func<T, Int32>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = _dbContext.Set<T>().OrderByDescending(Int32sortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                            else
                                dynamicList = _dbContext.Set<T>().OrderBy(Int32sortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(Int32sortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                            else
                                dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderBy(Int32sortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                        }
                    }
                    else
                    {
                        var Int32NullsortExpression = Expression.Lambda<Func<T, Int32?>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = _dbContext.Set<T>().OrderByDescending(Int32NullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                            else
                                dynamicList = _dbContext.Set<T>().OrderBy(Int32NullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(Int32NullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                            else
                                dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderBy(Int32NullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                        }
                    }
                    break;
                #endregion
                case TypeCode.String:
                    #region StringObject
                    var stringSortExpression = Expression.Lambda<Func<T, string>>(Objectparent, Objectparam);
                    if (predicate == null)
                    {
                        if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                            dynamicList = _dbContext.Set<T>().OrderByDescending(stringSortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                        else if (strSortType.ToLower() == "pdesc")
                            dynamicList = _dbContext.Set<T>().OrderByDescending(stringSortExpression).AsQueryable().Select(SelectedColumns);
                        else if (strSortType.ToLower() == "pasc")
                            dynamicList = _dbContext.Set<T>().OrderBy(stringSortExpression).AsQueryable().Select(SelectedColumns);
                        else
                            dynamicList = _dbContext.Set<T>().OrderBy(stringSortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(strSortType))
                        {

                            if (strSortType.ToLower() == "desc")
                                dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(stringSortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                            else if (strSortType.ToLower() == "pdesc")
                                dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(stringSortExpression).AsQueryable().Select(SelectedColumns);
                            else if (strSortType.ToLower() == "pasc")
                                dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderBy(stringSortExpression).AsQueryable().Select(SelectedColumns);
                            else
                                dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderBy(stringSortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                        }
                    }
                    break;
                #endregion
                default:
                    #region Object

                    var objectsortExpression = Expression.Lambda<Func<T, object>>(Objectparent, Objectparam);
                    if (predicate == null)
                    {
                        if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                            dynamicList = _dbContext.Set<T>().OrderByDescending(objectsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                        else
                            dynamicList = _dbContext.Set<T>().OrderBy(objectsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                            dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(objectsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                        else
                            dynamicList = _dbContext.Set<T>().Where<T>(predicate).OrderBy(objectsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size);
                    }
                    break;
                    #endregion
            }
            keyValuePairsDto = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(JsonConvert.SerializeObject(dynamicList));


            return convertUTCtoLocalDateTimeDictinory<T>(keyValuePairsDto, UtcToLocalProperty).Select(x => { x = x.ToDictionary(xd => xd.Key.Substring(xd.Key.IndexOf('_') + 1), xd => xd.Value); return x; }).ToList();//Remove Prefix from Key;
        }
        public async Task<List<Dictionary<string, object>>> GetManyWithSelectedColumnsAsync<T>(Expression<Func<T, bool>> predicate, string SelectedColumns, string UtcToLocalProperty = "") where T : class
        {
            dynamic dynamicList;
            List<Dictionary<string, object>> keyValuePairsDto = new List<Dictionary<string, object>>();

            SelectedColumns = "new(" + SelectedColumns + ")";
            if (predicate == null)
                dynamicList = await _dbContext.Set<T>().AsQueryable().Select(SelectedColumns).ToListAsync();
            else
                dynamicList = await _dbContext.Set<T>().Where<T>(predicate).AsQueryable().Select(SelectedColumns).ToListAsync();

            keyValuePairsDto = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(JsonConvert.SerializeObject(dynamicList));

            return convertUTCtoLocalDateTimeDictinory<T>(keyValuePairsDto, UtcToLocalProperty).Select(x => { x = x.ToDictionary(xd => xd.Key.Substring(xd.Key.IndexOf('_') + 1), xd => xd.Value); return x; }).ToList();//Remove Prefix from Key;
        }
        public async Task<List<Dictionary<string, object>>> GetManyWithSelectedColumnsAsync<T>(Expression<Func<T, bool>> predicate, string SelectedColumns, DbContext _Ctx, DbContextTransaction trx, string UtcToLocalProperty = "") where T : class
        {
            dynamic dynamicList;
            List<Dictionary<string, object>> keyValuePairsDto = new List<Dictionary<string, object>>();

            SelectedColumns = "new(" + SelectedColumns + ")";

            using (DbContext _DC = SetTransaction(_Ctx, trx))
            {
                _DC.Database.UseTransaction(trx.UnderlyingTransaction);
                if (predicate == null)
                    dynamicList = await _DC.Set<T>().AsQueryable().Select(SelectedColumns).ToListAsync();
                else
                    dynamicList = await _DC.Set<T>().Where<T>(predicate).AsQueryable().Select(SelectedColumns).ToListAsync();

                keyValuePairsDto = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(JsonConvert.SerializeObject(dynamicList));

                return convertUTCtoLocalDateTimeDictinory<T>(keyValuePairsDto, UtcToLocalProperty).Select(x => { x = x.ToDictionary(xd => xd.Key.Substring(xd.Key.IndexOf('_') + 1), xd => xd.Value); return x; }).ToList();//Remove Prefix from Key;
            }

        }
        public async Task<List<Dictionary<string, object>>> GetManyWithSelectedColumnsAsync<T>(Expression<Func<T, bool>> predicate, string SelectedColumns, int index, int size, string strSortColumn, string strSortType, string UtcToLocalProperty = "") where T : class
        {
            dynamic dynamicList = null;
            List<Dictionary<string, object>> keyValuePairsDto = new List<Dictionary<string, object>>();

            Type Type = typeof(T);
            Type propertyType = default(Type);
            PropertyInfo PropertyInfo;
            if (string.IsNullOrEmpty(strSortColumn))
            {
                //var sortcolumn = Type.GetProperties().ToList().Select(x => x.Name).FirstOrDefault();
                //var sortcolumn = Type.GetProperties().Where(x => x.PropertyType.FullName != "System.Guid").ToList().Select(x => x.Name).FirstOrDefault();
                //var sortcolumn = Type.GetProperties().Where(x => x.Name.Contains("CreatedDateTime")).Select(x => x.Name).ToString();
                //var sortcolumn = Type.GetProperties().Where(x => x.PropertyType.FullName != "System.Guid" && !(x.PropertyType.IsGenericType
                //&& x.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>) && x.PropertyType.GetGenericArguments()[0].Name == "Guid")).FirstOrDefault();

                //var sortcolumn = Type.GetProperties().Where(x => x.PropertyType.FullName != "System.Guid" && !(x.PropertyType.IsGenericType
                //&& x.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>)
                //&& x.PropertyType.GetGenericArguments()[0].Name == "Guid")).ToList().Select(x => x.Name).FirstOrDefault();

                var sortcolumn = Type.GetProperties().Where(x => x.PropertyType.FullName != "System.Guid" && x.PropertyType.FullName != "System.Boolean" && !(x.PropertyType.IsGenericType
                && x.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>)
                && x.PropertyType.GetGenericArguments()[0].Name == "Guid")).ToList().Select(x => x.Name).FirstOrDefault();

                PropertyInfo = Type.GetProperty(sortcolumn);
                strSortColumn = sortcolumn;
                strSortType = "Asc";
            }
            else
                PropertyInfo = Type.GetProperty(strSortColumn);
            propertyType = PropertyInfo.PropertyType;
            TypeCode typeCode = Type.GetTypeCode(PropertyInfo.PropertyType.GetTypeInfo());
            SelectedColumns = "new(" + SelectedColumns + ")";

            var Objectparam = Expression.Parameter(typeof(T), "op");
            Expression Objectparent = Expression.Property(Objectparam, strSortColumn);

            bool isNull = false;
            if (propertyType.IsGenericType &&
                    propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                isNull = true;
                propertyType = propertyType.GetGenericArguments()[0];
            }
            typeCode = Type.GetTypeCode(propertyType.GetTypeInfo());


            switch (typeCode)
            {
                case TypeCode.DateTime:
                    #region DateTimeObject
                    if (!isNull)
                    {
                        var DateTimesortExpression = Expression.Lambda<Func<T, DateTime>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = await _dbContext.Set<T>().OrderByDescending(DateTimesortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                            else
                                dynamicList = await _dbContext.Set<T>().OrderBy(DateTimesortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(DateTimesortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                            else
                                dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderBy(DateTimesortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                        }
                    }
                    else
                    {
                        var DateTimenullsortExpression = Expression.Lambda<Func<T, DateTime?>>(Objectparent, Objectparam);

                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = await _dbContext.Set<T>().OrderByDescending(DateTimenullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                            else
                                dynamicList = await _dbContext.Set<T>().OrderBy(DateTimenullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(DateTimenullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                            else
                                dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderBy(DateTimenullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                        }
                    }
                    break;
                #endregion
                case TypeCode.Decimal:
                    #region DecimalObject
                    if (!isNull)
                    {
                        var DecimalsortExpression = Expression.Lambda<Func<T, Decimal>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = await _dbContext.Set<T>().OrderByDescending(DecimalsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                            else
                                dynamicList = await _dbContext.Set<T>().OrderBy(DecimalsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(DecimalsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                            else
                                dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderBy(DecimalsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                        }
                    }
                    else
                    {
                        var DecimalNullsortExpression = Expression.Lambda<Func<T, Decimal?>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = await _dbContext.Set<T>().OrderByDescending(DecimalNullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                            else
                                dynamicList = await _dbContext.Set<T>().OrderBy(DecimalNullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(DecimalNullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                            else
                                dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderBy(DecimalNullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                        }

                    }
                    break;
                #endregion
                case TypeCode.Int64:
                    #region Int64Object
                    if (!isNull)
                    {
                        var Int64sortExpression = Expression.Lambda<Func<T, Int64>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = await _dbContext.Set<T>().OrderByDescending(Int64sortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                            else
                                dynamicList = await _dbContext.Set<T>().OrderBy(Int64sortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(Int64sortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                            else
                                dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderBy(Int64sortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                        }
                    }
                    else
                    {
                        var Int64NullsortExpression = Expression.Lambda<Func<T, Int64?>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = await _dbContext.Set<T>().OrderByDescending(Int64NullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                            else
                                dynamicList = await _dbContext.Set<T>().OrderBy(Int64NullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(Int64NullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                            else
                                dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderBy(Int64NullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                        }
                    }
                    break;
                #endregion
                case TypeCode.Int32:
                    #region Int32Object
                    if (!isNull)
                    {
                        var Int32sortExpression = Expression.Lambda<Func<T, Int32>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = await _dbContext.Set<T>().OrderByDescending(Int32sortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                            else
                                dynamicList = await _dbContext.Set<T>().OrderBy(Int32sortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(Int32sortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                            else
                                dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderBy(Int32sortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                        }
                    }
                    else
                    {
                        var Int32NullsortExpression = Expression.Lambda<Func<T, Int32?>>(Objectparent, Objectparam);
                        if (predicate == null)
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = await _dbContext.Set<T>().OrderByDescending(Int32NullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                            else
                                dynamicList = await _dbContext.Set<T>().OrderBy(Int32NullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                                dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(Int32NullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                            else
                                dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderBy(Int32NullsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                        }
                    }
                    break;
                #endregion
                case TypeCode.String:
                    #region StringObject
                    var stringSortExpression = Expression.Lambda<Func<T, string>>(Objectparent, Objectparam);
                    if (predicate == null)
                    {
                        if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                            dynamicList = await _dbContext.Set<T>().OrderByDescending(stringSortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                        else if (strSortType.ToLower() == "pdesc")
                            dynamicList = await _dbContext.Set<T>().OrderByDescending(stringSortExpression).AsQueryable().Select(SelectedColumns).ToListAsync();
                        else if (strSortType.ToLower() == "pasc")
                            dynamicList = await _dbContext.Set<T>().OrderBy(stringSortExpression).AsQueryable().Select(SelectedColumns).ToListAsync();
                        else
                            dynamicList = await _dbContext.Set<T>().OrderBy(stringSortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(strSortType))
                        {

                            if (strSortType.ToLower() == "desc")
                                dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(stringSortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                            else if (strSortType.ToLower() == "pdesc")
                                dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(stringSortExpression).AsQueryable().Select(SelectedColumns).ToListAsync();
                            else if (strSortType.ToLower() == "pasc")
                                dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderBy(stringSortExpression).AsQueryable().Select(SelectedColumns).ToListAsync();
                            else
                                dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderBy(stringSortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                        }
                    }
                    break;
                #endregion
                default:
                    #region Object

                    var objectsortExpression = Expression.Lambda<Func<T, object>>(Objectparent, Objectparam);
                    if (predicate == null)
                    {
                        if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                            dynamicList = await _dbContext.Set<T>().OrderByDescending(objectsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                        else
                            dynamicList = await _dbContext.Set<T>().OrderBy(objectsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(strSortType) && strSortType.ToLower() == "desc")
                            dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderByDescending(objectsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                        else
                            dynamicList = await _dbContext.Set<T>().Where<T>(predicate).OrderBy(objectsortExpression).AsQueryable().Select(SelectedColumns).Skip((index - 1) * size).Take(size).ToListAsync();
                    }
                    break;
                    #endregion
            }
            keyValuePairsDto = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(JsonConvert.SerializeObject(dynamicList));


            return convertUTCtoLocalDateTimeDictinory<T>(keyValuePairsDto, UtcToLocalProperty).Select(x => { x = x.ToDictionary(xd => xd.Key.Substring(xd.Key.IndexOf('_') + 1), xd => xd.Value); return x; }).ToList();//Remove Prefix from Key;
        }
        public List<Dictionary<string, object>> GetManyWithSelectedColumnsWithWhereClass<T>(string TableName, string SelectedColumns, string whereClause, string UtcToLocalProperty = "") where T : class
        {
            dynamic dynamicList = new ExpandoObject();
            List<Dictionary<string, object>> keyValuePairsDto = new List<Dictionary<string, object>>();
            DataTable dataTable = new DataTable();

            string Query = "select " + SelectedColumns + " from " + TableName + "(NOLOCK) where " + whereClause;

            dataTable = ExecuteQuery(Query.Replace("[Extent1].", ""));

            dynamicList = dataTable.AsEnumerable().Select(
           row => dataTable.Columns.Cast<DataColumn>().ToDictionary(
                    column => column.ColumnName,
                    column => row[column]))
              .ToList();


            keyValuePairsDto = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(JsonConvert.SerializeObject(dynamicList));

            return convertUTCtoLocalDateTimeDictinory<T>(keyValuePairsDto, UtcToLocalProperty).Select(x => { x = x.ToDictionary(xd => xd.Key, xd => xd.Value); return x; }).ToList();//Remove Prefix from Key;
        }
        public List<Dictionary<string, object>> GetManyWithSelectedColumnsWithWhereClass<T>(Expression<Func<T, bool>> predicate, string TableName, string SelectedColumns, string UtcToLocalProperty = "") where T : class
        {
            dynamic dynamicList = new ExpandoObject();
            List<Dictionary<string, object>> keyValuePairsDto = new List<Dictionary<string, object>>();
            DataTable dataTable = new DataTable();
            string fullQuery = _dbContext.Set<T>().Where(predicate).ToString();
            string[] whereQuery = Regex.Split(fullQuery, @"WHERE");
            string queryStr = whereQuery.Length > 1 ? whereQuery[1] : " (1=1)";

            string Query = "select " + SelectedColumns + " from " + TableName + " where " + queryStr;

            dataTable = ExecuteQuery(Query.Replace("[Extent1].", ""));

            dynamicList = dataTable.AsEnumerable().Select(
           row => dataTable.Columns.Cast<DataColumn>().ToDictionary(
                    column => column.ColumnName,
                    column => row[column]))
              .ToList();


            keyValuePairsDto = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(JsonConvert.SerializeObject(dynamicList));

            return convertUTCtoLocalDateTimeDictinory<T>(keyValuePairsDto, UtcToLocalProperty).Select(x => { x = x.ToDictionary(xd => xd.Key.Substring(xd.Key.IndexOf('_') + 1), xd => xd.Value); return x; }).ToList();//Remove Prefix from Key;
        }

        #endregion

        #region  Horizontal FindAll (SP Query)
        public List<Dictionary<string, object>> GetManyWithSelectedColumnsAndHorizontal<T>(Expression<Func<T, bool>> predicate, string PrimaryTable, string SelectedColumns, int index, int size, string strSortColumn, string strSortType, string UtcToLocalProperty = "") where T : class
        {
            dynamic dynamicList = new ExpandoObject();
            List<Dictionary<string, object>> keyValuePairsDto = new List<Dictionary<string, object>>();
            List<string> selColumnLst = SelectedColumns.Split(',').ToList();
            int pkIndex = selColumnLst.FindIndex(x => x.Contains("PK"));
            string PK = string.IsNullOrEmpty(selColumnLst.Where(x => x.Contains("PK")).FirstOrDefault()) ? selColumnLst[0].Split('_')[0] + "_PK" : selColumnLst.Where(x => x.Contains("PK")).FirstOrDefault().ToString();
            if (pkIndex >= 0)
                selColumnLst.RemoveAt(pkIndex);
            selColumnLst.Insert(0, PK);
            var indexLst = selColumnLst.Where(i => i.Contains("__Actual") || i.Contains("__Due")).ToList();
            selColumnLst = selColumnLst.Except(indexLst).ToList();

            string fullQuery = _dbContext.Set<T>().Where(predicate).ToString();
            string[] whereQuery = Regex.Split(fullQuery, @"WHERE");

            string queryStr = whereQuery.Length > 1 ? whereQuery[1] : " (1=1)";

            var pPrimaryTable = new SqlParameter("@PrimaryTable", PrimaryTable);
            var pSelectedColumn = new SqlParameter("@TableCols", string.Join(",", selColumnLst));
            var pStartRow = new SqlParameter("@StartRow", ((index - 1) * size) + 1);
            var pEndRow = new SqlParameter("@EndRow", (index - 1) * size + size);
            var pSortColumn = new SqlParameter("@SortColumn", strSortColumn);
            var pSortType = new SqlParameter("@SortType", strSortType);
            var pWhereQuery = new SqlParameter("@WhereQuery", queryStr);
            Object[] parameters = new Object[7];
            parameters[0] = pPrimaryTable;
            parameters[1] = pSelectedColumn;
            parameters[2] = pStartRow;
            parameters[3] = pEndRow;
            parameters[4] = pSortColumn;
            parameters[5] = pSortType;
            parameters[6] = pWhereQuery;
            DataTable dtTable = ExecuteStoredProcedure("SP_Horizontal_View", parameters);

            dynamicList = dtTable.AsEnumerable().Select(
             row => dtTable.Columns.Cast<DataColumn>().ToDictionary(
        column => column.ColumnName,
        column => row[column]))
                .ToList();


            keyValuePairsDto = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(JsonConvert.SerializeObject(dynamicList));

            //return keyValuePairsDto.Select(x => { x = x.ToDictionary(xd => xd.Key.Substring(xd.Key.IndexOf('_') + 1), xd => xd.Value); return x; }).ToList();//Remove Prefix from Key;

            return convertUTCtoLocalDateTimeDictinory<T>(keyValuePairsDto, UtcToLocalProperty).Select(x => { x = x.ToDictionary(xd => xd.Key.Substring(xd.Key.IndexOf('_') + 1), xd => xd.Value); return x; }).ToList();//Remove Prefix from Key;
        }

        public DataTable GetManyWithWhereClass<T>(Expression<Func<T, bool>> predicate, string TableName, int index = 0, int size = 0, string strSortColumn = null, string strSortType = null) where T : class
        {
            DataTable dataTable = new DataTable();
            string fullQuery = _dbContext.Set<T>().Where(predicate).ToString();
            string[] whereQuery = Regex.Split(fullQuery, @"WHERE");
            string queryStr = whereQuery.Length > 1 ? whereQuery[1] : " (1=1)";
            string Query = "select * from " + TableName + " where " + queryStr;
            if (!string.IsNullOrEmpty(strSortColumn) && !string.IsNullOrEmpty(strSortType))
                Query = Query + " Order by  " + strSortColumn + " " + strSortType;
            dataTable = ExecuteQuery(Query.Replace("[Extent1].", ""));
            if (dataTable.Rows.Count > 0 && index > 0 && size > 0)
                return dataTable = dataTable.Select().Skip((((index - 1) * size) + 1)).Take(size).CopyToDataTable();
            else
                return dataTable;
        }
        public List<Dictionary<string, object>> GetManyWithSelectedColumnsAndHorizontalCount<T>(Expression<Func<T, bool>> predicate, string PrimaryTable, string SelectedColumns, int index, int size, string strSortColumn, string strSortType) where T : class
        {
            dynamic dynamicList = new ExpandoObject();
            List<Dictionary<string, object>> keyValuePairsDto = new List<Dictionary<string, object>>();
            List<string> selColumnLst = SelectedColumns.Split(',').ToList();
            int pkIndex = selColumnLst.FindIndex(x => x.Contains("PK"));
            string PK = string.IsNullOrEmpty(selColumnLst.Where(x => x.Contains("PK")).FirstOrDefault()) ? selColumnLst[0].Split('_')[0] + "_PK" : selColumnLst.Where(x => x.Contains("PK")).FirstOrDefault().ToString();
            if (pkIndex >= 0)
                selColumnLst.RemoveAt(pkIndex);
            selColumnLst.Insert(0, PK);
            var indexLst = selColumnLst.Where(i => i.Contains("__Actual") || i.Contains("__Due")).ToList();
            selColumnLst = selColumnLst.Except(indexLst).ToList();

            string fullQuery = _dbContext.Set<T>().Where(predicate).ToString();
            string[] whereQuery = Regex.Split(fullQuery, @"WHERE");

            string queryStr = whereQuery.Length > 1 ? whereQuery[1] : " (1=1)";

            var pPrimaryTable = new SqlParameter("@PrimaryTable", PrimaryTable);
            var pSelectedColumn = new SqlParameter("@TableCols", string.Join(",", selColumnLst));
            var pStartRow = new SqlParameter("@StartRow", (index - 1) * size);
            var pEndRow = new SqlParameter("@EndRow", (index - 1) * size + size);
            var pSortColumn = new SqlParameter("@SortColumn", strSortColumn);
            var pSortType = new SqlParameter("@SortType", strSortType);
            var pWhereQuery = new SqlParameter("@WhereQuery", queryStr);
            Object[] parameters = new Object[7];
            parameters[0] = pPrimaryTable;
            parameters[1] = pSelectedColumn;
            parameters[2] = pStartRow;
            parameters[3] = pEndRow;
            parameters[4] = pSortColumn;
            parameters[5] = pSortType;
            parameters[6] = pWhereQuery;
            DataTable dtTable = ExecuteStoredProcedure("SP_Horizontal_View_Count", parameters);

            dynamicList = dtTable.AsEnumerable().Select(
             row => dtTable.Columns.Cast<DataColumn>().ToDictionary(
        column => column.ColumnName,
        column => row[column].ToString()))
                .ToList();

            keyValuePairsDto = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(JsonConvert.SerializeObject(dynamicList));

            return keyValuePairsDto.Select(x => { x = x.ToDictionary(xd => xd.Key.Substring(xd.Key.IndexOf('_') + 1), xd => xd.Value); return x; }).ToList();//Remove Prefix from Key;
        }
        #endregion


        #region Horizontal Get By Id (SP Query)
        public Dictionary<string, object> GetByIdWithHorizontal<T>(Expression<Func<T, bool>> predicate, string PrimaryTable, string Table_PK, string UtcToLocalProperty = "") where T : class
        {
            dynamic dynamicList = new ExpandoObject();
            List<Dictionary<string, object>> keyValuePairsDto = new List<Dictionary<string, object>>();
            List<string> selColumnLst = new List<string> { Table_PK };


            Type t = typeof(T);
            PropertyInfo[] props = t.GetProperties();
            Dictionary<string, object> dict = new Dictionary<string, object>();


            foreach (PropertyInfo prp in props)
            {
                if (!selColumnLst.Contains(prp.Name) && prp.CustomAttributes.Where(x => x.AttributeType.Name == "NotMappedAttribute").Count() == 0)
                    selColumnLst.Add(prp.Name);
            }


            string fullQuery = _dbContext.Set<T>().Where(predicate).ToString();
            string[] whereQuery = Regex.Split(fullQuery, @"WHERE");

            string queryStr = whereQuery.Length > 1 ? whereQuery[1] : " (1=1)";


            var pPrimaryTable = new SqlParameter("@PrimaryTable", PrimaryTable);
            var pSelectedColumn = new SqlParameter("@TableCols", string.Join(",", selColumnLst));
            var pStartRow = new SqlParameter("@StartRow", 1);
            var pEndRow = new SqlParameter("@EndRow", 1000);
            var pSortColumn = new SqlParameter("@SortColumn", "");
            var pSortType = new SqlParameter("@SortType", "ASC");
            var pWhereQuery = new SqlParameter("@WhereQuery", queryStr);
            Object[] parameters = new Object[7];
            parameters[0] = pPrimaryTable;
            parameters[1] = pSelectedColumn;
            parameters[2] = pStartRow;
            parameters[3] = pEndRow;
            parameters[4] = pSortColumn;
            parameters[5] = pSortType;
            parameters[6] = pWhereQuery;
            DataTable dtTable = ExecuteStoredProcedure("SP_Horizontal_View", parameters);

            dynamicList = dtTable.AsEnumerable().Select(
            row => dtTable.Columns.Cast<DataColumn>().ToDictionary(
            column => column.ColumnName,
            column => row[column].ToString()))
            .ToList();

            keyValuePairsDto = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(JsonConvert.SerializeObject(dynamicList));

            //return keyValuePairsDto.Select(x => { x = x.ToDictionary(xd => xd.Key.Substring(xd.Key.IndexOf('_') + 1), xd => xd.Value); return x; }).FirstOrDefault();
            return convertUTCtoLocalDateTimeDictinory<T>(keyValuePairsDto, UtcToLocalProperty).Select(x => { x = x.ToDictionary(xd => xd.Key.Substring(xd.Key.IndexOf('_') + 1), xd => xd.Value); return x; }).FirstOrDefault();//Remove Prefix from Key;
        }
        #endregion        

        #region Execute SP/Function/Querys

        public virtual List<T> ExecuteStandardQuery<T>(string strQuery) where T : class
        {
            var connectionString = _dbContext.Database.Connection.ConnectionString.ToString();
            var ds = new DataSet();
            List<T> Temp = new List<T>();
            using (var conn = new SqlConnection(connectionString))
            {
                try
                {
                    using (SqlCommand cmd = new SqlCommand(strQuery, conn))
                    using (var adapter = new SqlDataAdapter(cmd))
                        adapter.Fill(ds);
                    Temp = ConvertTo<T>(ds.Tables[0]);
                }
                catch (Exception)
                {

                    throw;
                }
                finally
                {
                    conn.Close();
                }
            }
            return Temp;
        }
        public virtual List<T> ExecuteStandardQueryTransaction<T>(string strQuery) where T : class
        {
            var connectionString = _dbContext.Database.Connection.ConnectionString.ToString();
            var ds = new DataSet();
            List<T> Temp = new List<T>();
            var conn = _dbContext.Database.Connection as SqlConnection;
            SqlTransaction tr = _dbContext.Database.CurrentTransaction.UnderlyingTransaction as SqlTransaction;

            using (SqlCommand c = new SqlCommand(strQuery, conn, tr))
            using (var adapter = new SqlDataAdapter(c))
                adapter.Fill(ds);
            Temp = ConvertTo<T>(ds.Tables[0]);

            return Temp;
        }
        public string ExecuteFunction(Object[] strInput, string strFunctionName)
        {
            string sqlQuery = "SELECT [dbo]." + strFunctionName;
            Object[] parameters = strInput;
            string strEmployeeCode = _dbContext.Database.SqlQuery<string>(sqlQuery, parameters).FirstOrDefault();
            return strEmployeeCode;
        }
        public DataTable ExecuteStoredProcedure(string storedProcedureName, Object[] parameters)
        {
            var connectionString = _dbContext.Database.Connection.ConnectionString.ToString();
            var ds = new DataSet();
            using (var conn = new SqlConnection(connectionString))
            {
                using (var cmd = conn.CreateCommand())
                {
                    try
                    {
                        cmd.CommandText = storedProcedureName;
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandTimeout = 600; // one line added
                        if (parameters != null)
                        {
                            foreach (var parameter in parameters)
                            {
                                cmd.Parameters.Add(parameter);
                            }
                        }
                        using (var adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(ds);
                        }
                    }
                    catch (Exception)
                    {

                        throw;
                    }
                }
            }
            return ds.Tables[0];
        }
        public void ExecuteStoredProcedure(string storedProcedureName)
        {
            var connectionString = _dbContext.Database.Connection.ConnectionString.ToString();
            var ds = new DataSet();
            using (var conn = new SqlConnection(connectionString))
            {
                using (var cmd = conn.CreateCommand())
                {
                    try
                    {
                        cmd.CommandText = storedProcedureName;
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.CommandTimeout = 600; // one line added
                        using (var adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(ds);
                        }
                    }
                    catch (Exception)
                    {

                        throw;
                    }
                }
            }
        }
        public DataSet ExecuteStoredProcedureDS(string storedProcedureName, Object[] parameters)
        {
            var connectionString = _dbContext.Database.Connection.ConnectionString.ToString();
            var ds = new DataSet();

            using (var conn = new SqlConnection(connectionString))
            {
                using (var cmd = conn.CreateCommand())
                {
                    try
                    {
                        cmd.CommandText = storedProcedureName;
                        cmd.CommandType = CommandType.StoredProcedure;
                        foreach (var parameter in parameters)
                        {
                            cmd.Parameters.Add(parameter);
                        }
                        using (var adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(ds);
                        }
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
            }
            return ds;
        }
        public object ExecuteStoredProcedureScaler(string storedProcedureName, Object[] parameters)
        {
            var connectionString = _dbContext.Database.Connection.ConnectionString.ToString();
            object result = new object();

            using (var conn = new SqlConnection(connectionString))
            {
                using (var cmd = conn.CreateCommand())
                {
                    try
                    {
                        cmd.CommandText = storedProcedureName;
                        cmd.CommandType = CommandType.StoredProcedure;
                        foreach (var parameter in parameters)
                        {
                            cmd.Parameters.Add(parameter);
                        }
                        conn.Open();

                        result = (object)cmd.ExecuteScalar();

                    }
                    catch (Exception)
                    {

                        throw;
                    }
                    finally
                    {
                        conn.Close();
                    }
                }
            }
            return result;
        }
        public DataTable ExecuteQuery(string strQuery)
        {
            var connectionString = _dbContext.Database.Connection.ConnectionString.ToString();
            var ds = new DataSet();

            using (var conn = new SqlConnection(connectionString))
            {
                try
                {
                    using (SqlCommand cmd = new SqlCommand(strQuery, conn))
                    using (var adapter = new SqlDataAdapter(cmd))
                    {
                        cmd.CommandTimeout = 600;
                        adapter.Fill(ds);
                    }
                }
                catch (Exception)
                {
                    JObject jObject = new JObject();
                    jObject.Add("ConnectionString", connectionString);
                    SQSLogHelper.SendQueueMessage(ConfigurationManager.AppSettings["DESIssueSQSName"], "Q |" + strQuery, JsonConvert.SerializeObject(jObject));
                    throw;
                }
                finally
                {
                    conn.Close();
                }

            }
            return ds.Tables[0];
        }
        public bool ExecuteCommandQuery(string strQuery)
        {
            bool result = false;
            using (var conn = (SqlConnection)_dbContext.Database.Connection)
            {
                try
                {
                    if (conn.State != ConnectionState.Open)
                        conn.Open();
                    using (SqlCommand cmd = new SqlCommand(strQuery, conn))
                    {
                        cmd.CommandType = CommandType.Text;
                        cmd.ExecuteNonQuery();
                        result = true;
                    }
                    conn.Close();
                }
                catch (Exception)
                {
                    JObject jObject = new JObject();
                    jObject.Add("ConnectionString", _dbContext.Database.Connection.ConnectionString.ToString());
                    SQSLogHelper.SendQueueMessage(ConfigurationManager.AppSettings["DESIssueSQSName"], "Q |" + strQuery, JsonConvert.SerializeObject(jObject));
                    throw;
                }
                finally
                {
                    conn.Close();
                }
            }
            return result;
        }
        public bool ExecuteCommandQueryList(List<string> strQuery)
        {
            var connectionString = _dbContext.Database.Connection.ConnectionString.ToString();
            bool result = false;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlTransaction transaction;
                transaction = conn.BeginTransaction("TansactionBegin");
                try
                {
                    foreach (var item in strQuery)
                    {
                        using (SqlCommand cmd = new SqlCommand(item.ToString(), conn))
                        {
                            cmd.Connection = conn;
                            cmd.Transaction = transaction;
                            cmd.CommandType = CommandType.Text;
                            cmd.ExecuteNonQuery();
                            result = true;
                        }
                    }
                    transaction.Commit();
                    conn.Close();
                }
                catch (Exception)
                {
                    transaction.Rollback();

                    throw;
                }
                finally
                {
                    conn.Close();
                }
            }
            return result;
        }
        public bool DeleteByIds(KeyValueEntity deleteInput)
        {
            string tName = deleteInput.TableName;
            StringBuilder queryBuilder = new StringBuilder();
            /*var obj = typeof(T);*/
            string guidList = string.Empty;
            ((List<Guid>)deleteInput.Value).ForEach(x => { guidList += "'" + x + "',"; });
            guidList = guidList.Substring(0, guidList.Length - 1);
            queryBuilder.Append("DELETE FROM " + tName + " WHERE " + deleteInput.Key + " IN (" + guidList + ")");
            return ExecuteCommandQuery(queryBuilder.ToString());
        }
        public DataTable GetSqlQuery(List<string> literal, string TableorViewName, string condition)
        {
            string selectClause = "SELECT ";
            if (literal == null || literal.Count == 0)
                selectClause += "*"; // Select all columns by default            
            else
                selectClause += string.Join(",", literal);
            object jsonValue = null;
            string[] RestrictedKeywords;
            ConfigHelper configHelper = new ConfigHelper(ConfigFileIndex.CommonConfig);
            bool isKeyExist = configHelper.TryGetValue("SqlInjection.RestrictedKeywords", out jsonValue);
            if (isKeyExist && jsonValue != null)
            {
                string jsonString = jsonValue.ToString(); // Assuming jsonValue is a string
                RestrictedKeywords = Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>(jsonString);
            }
            else
            {
                throw new Exception("Required Key or Json does not exist(for query formation).");
            }
            if (ContainsRestrictedKeywords(condition, RestrictedKeywords))
                throw new ArgumentException("Condition contains disallowed characters or keywords.");
            string sqlQuery = $"{selectClause} FROM {TableorViewName} WHERE({condition})";
            return ExecuteQuery(sqlQuery);
        }
        private bool ContainsRestrictedKeywords(string input, string[] RestrictedKeywords)
        {
            // Split the input into words using space as a delimiter
            string[] words = input.Split(' ');
            // Check for restricted keywords, ignoring words inside single quotes
            return words.Any(word =>
                !(word.StartsWith("'") && word.EndsWith("'")) &&
                RestrictedKeywords.Any(keyword =>
                    word.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        #endregion

        #region Entity Based Check/Add Value/Read/Remove/Valid Check

        private List<Dictionary<string, object>> convertUTCtoLocalDateTimeDictinory<T>(List<Dictionary<string, object>> lstDictionary, string UtcToLocal = "") where T : class
        {
            Type Type = typeof(T);
            List<Dictionary<string, object>> keyValuePairs = new List<Dictionary<string, object>>();
            Dictionary<string, object> keyValue = null;
            List<string> lstfieldName = new List<string>();
            if (!string.IsNullOrEmpty(UtcToLocal))
                lstfieldName = UtcToLocal.Split(new char[] { ',' }).ToList();
            if (lstfieldName != null && lstfieldName.Count > 0)
            {
                lstDictionary.ToList().ForEach(mx =>
                {
                    keyValue = new Dictionary<string, object>();
                    mx.ToList().ForEach(x =>
                    {
                        var dateValue = false;
                        if (Type.GetProperty(x.Key) != null)// Else block not required for this if case. Date value will be false as default
                        {
                            var propertyType = Type.GetProperty(x.Key).PropertyType;
                            dateValue = propertyType.FullName.Contains("System.DateTime");
                        }
                        if (dateValue)
                        {
                            if (lstfieldName.Exists(y => y.Equals(x.Key)))
                            {
                                TokenClaims tc = (TokenClaims)System.Web.HttpContext.Current.Items["tokens"];
                                int timezoneOffset = 0;
                                int additionValue = 1;
                                string timezoneValue = string.Empty;
                                bool intCheck = int.TryParse(tc.UserDefinedTimezone, out timezoneOffset);
                                if (intCheck)
                                {
                                    additionValue = -1;
                                    timezoneValue = tc.UserDefinedTimezone;
                                }
                                else
                                    timezoneValue = TimeZoneInfo.FindSystemTimeZoneById(tc.UserDefinedTimezone).BaseUtcOffset.TotalMinutes.ToString();
                                if (x.Value != null)
                                {
                                    if (tc != null && !string.IsNullOrEmpty(tc.UserDefinedTimezone) && (intCheck || int.TryParse(TimeZoneInfo.FindSystemTimeZoneById(tc.UserDefinedTimezone).BaseUtcOffset.TotalMinutes.ToString(), out timezoneOffset)))
                                        keyValue[x.Key] = Convert.ToDateTime(x.Value).AddMinutes(!string.IsNullOrEmpty(tc.UserDefinedTimezone) ? additionValue * Convert.ToDouble(timezoneValue) : 330);
                                    else
                                        keyValue[x.Key] = Convert.ToDateTime(x.Value);

                                }
                                else
                                    keyValue[x.Key] = x.Value;
                            }
                            else
                            {
                                keyValue[x.Key] = x.Value; ;
                            }
                        }
                        else
                        {
                            keyValue[x.Key] = x.Value; ;
                        }
                    });
                    keyValuePairs.Add(keyValue);
                });
                return keyValuePairs;
            }
            else
                return lstDictionary;
        }
        public virtual bool IsValid<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            bool IsValid = false;
            int count = 0;
            if (predicate != null)
            {
                count = _dbContext.Set<T>().Where<T>(predicate).Count();
                if (count > 0)
                    IsValid = false;
                else
                    IsValid = true;
            }
            else
                IsValid = false;

            return IsValid;
        }
        public virtual bool IsValid<T>(string FieldName, string FieldValue) where T : class
        {
            bool IsValid = false;
            int count = 0;

            ExpressionType itemComparison = ExpressionType.Equal;

            ParameterExpression param = Expression.Parameter(typeof(T), "e");

            var collection = Expression.Property(param, FieldName);

            var predicate = Expression.Lambda<Func<T, bool>>(
               Expression.MakeBinary(itemComparison, collection, Expression.Constant(
                   string.IsNullOrEmpty(FieldValue) || FieldValue.Equals("null", StringComparison.OrdinalIgnoreCase) ? null :
                   Convert.ChangeType(FieldValue, collection.Type))),
               param);

            if (predicate != null)
            {
                count = _dbContext.Set<T>().Where<T>(predicate).Count();
                if (count > 0)
                    IsValid = false;
                else
                    IsValid = true;
            }
            else
                IsValid = false;

            return IsValid;

        }
        public List<object> SelectPropertywithFilter<T>(string propertyName, Expression<Func<T, bool>> predicate) where T : class
        {
            try
            {
                IQueryable<T> query;
                var entities = _dbContext.Set<T>();
                if (predicate != null)
                    query = entities.AsQueryable().Where<T>(predicate);
                else
                    query = entities.AsQueryable();
                var parameter = Expression.Parameter(typeof(T), "instance");
                var propertyAccess = Expression.Property(parameter, propertyName);
                var projection = Expression.Lambda(propertyAccess, parameter);
                ParameterExpression pe = Expression.Parameter(typeof(T), "x");

                Expression selectExpression = Expression.Call(
                    typeof(Queryable).GetMethods()
                                     .First(x => x.Name == "Select")
                                     .MakeGenericMethod(new[] { typeof(T), propertyAccess.Type }),
                    query.Expression,
                    projection);


                Expression coalesce = Expression.Coalesce(Expression.PropertyOrField(parameter, propertyName),
                            Expression.Constant(string.Empty));

                Expression InitialExp;
                InitialExp = Expression.Call(coalesce, typeof(string).GetMethod("Contains"), Expression.Constant(propertyName));

                Expression distinctExpression = Expression.Call(
                    typeof(Queryable).GetMethods()
                                     .First(x => x.Name == "Distinct")
                                     .MakeGenericMethod(new[] { propertyAccess.Type }),
                    selectExpression);


                IQueryable<object> QueryList = (IQueryable<object>)query.Provider.CreateQuery(distinctExpression);
                List<object> Result = QueryList.ToList();
                Result = Result.Where(i => i != null).ToList();
                return Result;
            }
            catch
            {
                Dispose();
                throw;
            }
        }
        public List<object> SelectProperty<T>(string propertyName) where T : class
        {
            try
            {
                var entities = _dbContext.Set<T>();
                var query = entities.AsQueryable();
                var parameter = Expression.Parameter(typeof(T), "instance");
                var propertyAccess = Expression.Property(parameter, propertyName);
                var projection = Expression.Lambda(propertyAccess, parameter);
                ParameterExpression pe = Expression.Parameter(typeof(string), "x");

                var selectExpression = Expression.Call(
                    typeof(Queryable).GetMethods()
                                     .First(x => x.Name == "Select")
                                     .MakeGenericMethod(new[] { typeof(T), propertyAccess.Type }),
                    query.Expression,
                    projection);

                var coalesce = Expression.Coalesce(Expression.PropertyOrField(parameter, propertyName),
                            Expression.Constant(string.Empty));

                Expression InitialExp;
                InitialExp = Expression.Call(coalesce, typeof(string).GetMethod("Contains"), Expression.Constant(propertyName));

                var distinctExpression = Expression.Call(
                    typeof(Queryable).GetMethods()
                                     .First(x => x.Name == "Distinct")
                                     .MakeGenericMethod(new[] { propertyAccess.Type }),
                    selectExpression);

                IQueryable<object> QueryList = (IQueryable<object>)query.Provider.CreateQuery(distinctExpression);
                List<object> Result = QueryList.ToList();
                Result = Result.Where(i => i != null).ToList();
                return Result;
            }
            catch
            {
                throw;
            }
        }
        private T SelectedUpdateEntityCheck<T>(T Entity, ColumnUpdate columnUpdate) where T : class
        {
            bool getByIDRequired = false;
            bool oldNewValueDiff = false;
            if (string.IsNullOrEmpty(columnUpdate.EntityRefPK) && columnUpdate.SequenceID == null && columnUpdate.SequenceID == 0)
                return null;
            if (Entity != null)
            {
                //oldNewValueDiff = columnUpdate.Properties.Where(x => x.PropertyNewValue != x.PropertyOldValue).Count() > 0 ? true : false;
                columnUpdate.Properties.ForEach(x =>
                {

                    if (!getByIDRequired && ((typeof(T).GetProperty(x.PropertyName).GetValue(Entity) == null && x.PropertyOldValue == null) || typeof(T).GetProperty(x.PropertyName).GetValue(Entity) != x.PropertyOldValue))
                    {
                        getByIDRequired = true;
                    }

                });
            }
            else
            {
                //oldNewValueDiff = true;
                getByIDRequired = true;
            }
            if (getByIDRequired)
            {
                var keyValue = LogQueueHelper.GetPrimaryKey<T>(Entity);
                string tableName = GetTableName<T>();
                if (!String.IsNullOrEmpty(columnUpdate.EntityRefPK))
                {
                    var dt = ExecuteQuery("SELECT * FROM " + tableName + "(NOLOCK) WHERE " + keyValue["Key"] + " = '" + columnUpdate.EntityRefPK + "'");
                    Entity = JsonConvert.DeserializeObject<List<T>>(JsonConvert.SerializeObject(dt)).FirstOrDefault();
                }
                if (columnUpdate.SequenceID != null && columnUpdate.SequenceID != 0)
                {
                    var dt = ExecuteQuery("SELECT * FROM " + tableName + "(NOLOCK) WHERE " + keyValue["Key"] + " = '" + columnUpdate.SequenceID + "'");
                    Entity = JsonConvert.DeserializeObject<List<T>>(JsonConvert.SerializeObject(dt)).FirstOrDefault();
                }

                if (Entity != null)
                {
                    columnUpdate.Properties.Where(x => x.PropertyOldValue != x.PropertyNewValue).ToList().ForEach(x =>
                    {
                        Type ptype = Entity.GetType().GetProperty(x.PropertyName).PropertyType;

                        if (ptype.IsGenericType && ptype.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
                        {
                            if (string.IsNullOrEmpty(Convert.ToString(x.PropertyNewValue)))
                                Entity.GetType().GetProperty(x.PropertyName).SetValue(Entity, null);
                            else if (ptype.FullName.Contains("System.Guid"))
                                Entity.GetType().GetProperty(x.PropertyName).SetValue(Entity, Guid.Parse(x.PropertyNewValue?.ToString()));
                            else
                                Entity.GetType().GetProperty(x.PropertyName).SetValue(Entity, Convert.ChangeType(x.PropertyNewValue, Nullable.GetUnderlyingType(ptype)));
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(Convert.ToString(x.PropertyNewValue)))
                                Entity.GetType().GetProperty(x.PropertyName).SetValue(Entity, null);
                            else if (ptype.FullName.Contains("System.Guid"))
                                Entity.GetType().GetProperty(x.PropertyName).SetValue(Entity, Guid.Parse(x.PropertyNewValue?.ToString()));
                            else if (x.PropertyNewValue != null && ptype.Name.Contains("Byte[]"))
                                Entity.GetType().GetProperty(x.PropertyName).SetValue(Entity, Guid.Parse(x.PropertyNewValue?.ToString()).ToByteArray());
                            else
                                Entity.GetType().GetProperty(x.PropertyName).SetValue(Entity, Convert.ChangeType(x.PropertyNewValue, ptype));
                        }
                    });
                }
            }
            else if (!oldNewValueDiff)
                return null;

            return Entity;
        }
        public virtual T SetValueToProperty<T>(T Entity, List<Property> Properties)
        {
            try
            {
                if (Properties != null)
                {
                    Type t = Entity.GetType();
                    foreach (Property item in Properties)
                    {
                        foreach (var propInfo in t.GetProperties())
                        {
                            if (propInfo.Name == item.PropertyName)
                            {
                                object PropValue = null;
                                if (item.PropertyNewValue != null)
                                {
                                    PropValue = TrustHelper.GetValueBasedonType(propInfo, item.PropertyNewValue.ToString());

                                }
                                if (propInfo.CanWrite)
                                    propInfo.SetValue(Entity, PropValue, null);

                                //Guid guidOutput = new Guid();
                                //Decimal decimalOutput = new decimal();
                                //DateTime dateTimeField = new DateTime();
                                //bool booleanField = new bool(); 
                                //bool isGuidUpdate = Guid.TryParse(item.PropertyNewValue.ToString(), out guidOutput);
                                //bool isDecimalUpdate = Decimal.TryParse(item.PropertyNewValue.ToString(), out decimalOutput);
                                //bool isDateUpdate = DateTime.TryParse(item.PropertyNewValue.ToString(), out dateTimeField);
                                //bool isBoolUpdate = Boolean.TryParse(item.PropertyNewValue.ToString(), out booleanField);
                                //if (isDateUpdate)
                                //    propInfo.SetValue(Entity, dateTimeField, null);
                                //else if (isGuidUpdate)
                                //    propInfo.SetValue(Entity, guidOutput, null);
                                //else if (isDecimalUpdate)
                                //    propInfo.SetValue(Entity, decimalOutput, null);
                                //else if (isBoolUpdate)
                                //    propInfo.SetValue(Entity, booleanField, null);
                                ////else if (isDateUpdate)
                                ////    propInfo.SetValue(Entity, dateTimeField, null);
                                //else
                                //    propInfo.SetValue(Entity, item.PropertyNewValue, null);

                                //break;


                            }
                        }
                    }
                }
                return Entity;
            }
            catch (Exception e)
            {
                throw;
            }
        }
        public Int32 CheckSharedEntityCount<T>(T TObject) where T : class
        {
            Int32 sharedEntityCount = 0;
            //if (TObject.GetType().Name.ToUpper() == "DATASHAREDENTITY")
            //    return null;               

            var _Instance = TrustFactory.GetAppInstance();

            //GETTING DATABASENAME
            string databaseName = _Instance.GetDataBaseName();

            //GETTING TABLENAME
            string tableName = _Instance.GetTableName<T>();

            //CHECK TABLE NAME EXIST IN QUEUE LOG LIST
            if (!TrustHelper.IsTableNameExist(tableName))
                return 0;

            List<CustomSharedEntity> CustomSettings = TrustHelper.ReadJsonConfiguration(tableName);
            if (CustomSettings != null)
                sharedEntityCount = CustomSettings.Count;
            return sharedEntityCount;
        }
        public virtual T AddVersionProperty<T>(T TObject) where T : class
        {
            if (TObject.GetType().GetProperty("TNT_Version") != null)
            {
                System.Reflection.PropertyInfo p = typeof(T).GetProperty("TNT_Version");
                if (p != null)
                {
                    System.Type t = p.PropertyType;

                    if (t == typeof(byte[]))
                    {
                        p.SetValue(TObject, Guid.NewGuid().ToByteArray());
                    }
                }
            }
            else if (TObject.GetType().GetProperty("CMN_Version") != null)
            {
                System.Reflection.PropertyInfo p = typeof(T).GetProperty("CMN_Version");
                if (p != null)
                {
                    System.Type t = p.PropertyType;

                    if (t == typeof(byte[]))
                    {
                        p.SetValue(TObject, Guid.NewGuid().ToByteArray());
                    }
                }
            }
            return TObject;
        }
        private static string GetUserName(string UserName)
        {
            if (HttpContext.Current != null)
            {
                foreach (var item in HttpContext.Current.Items)
                {
                    TokenClaims tc = new TokenClaims();
                    if (((System.Collections.DictionaryEntry)(item)).Key.ToString() == "tokens")
                    {
                        tc = (TokenClaims)((System.Collections.DictionaryEntry)(item)).Value;
                        if (tc != null)
                        {
                            UserName = tc.UserName;
                            break;
                        }
                    }
                }
            }
            return UserName;
        }


        public virtual T AddTenentCodeProperty<T>(T TObject) where T : class
        {
            var tenantCode = GetTenantCode();
            if (!string.IsNullOrEmpty(tenantCode))
            {
                if (TObject.GetType().GetProperty("TNT_TenantCode") != null)
                {
                    System.Reflection.PropertyInfo p = typeof(T).GetProperty("TNT_TenantCode");
                    if (p != null)
                    {
                        System.Type t = p.PropertyType;

                        if (t == typeof(string))
                        {
                            p.SetValue(TObject, tenantCode);
                        }
                    }
                }
                else if (TObject.GetType().GetProperty("CMN_TenantCode") != null)
                {
                    System.Reflection.PropertyInfo p = typeof(T).GetProperty("CMN_TenantCode");
                    if (p != null)
                    {
                        System.Type t = p.PropertyType;
                        if (t == typeof(string))
                        {
                            p.SetValue(TObject, tenantCode);
                        }
                    }
                }
            }
            return TObject;
        }
        private static string GetTenantCode()
        {
            var TenantCode = string.Empty;
            TokenClaims _tc = TrustHelper.GetTokenCliams();
            if (_tc.UserName != null)
                TenantCode = _tc.TenantCode;
            else if (HttpContext.Current != null)
            {

                foreach (var item in HttpContext.Current.Items)
                {
                    TokenClaims tc = new TokenClaims();
                    if (((System.Collections.DictionaryEntry)(item)).Key.ToString() == "tokens")
                    {
                        tc = (TokenClaims)((System.Collections.DictionaryEntry)(item)).Value;
                        if (tc != null)
                        {
                            TenantCode = tc.TenantCode;
                            break;
                        }
                    }
                }
            }
            return TenantCode;
        }
        public virtual bool CheckTenantValid<T>(T TObject)
        {
            bool IsValid = true;
            if (TObject.GetType().GetProperty("TNT_TenantCode") != null)
            {
                if (!string.IsNullOrEmpty((string)TObject.GetType().GetProperty("TNT_TenantCode").GetValue(TObject)))
                    IsValid = false;
            }
            if (TObject.GetType().GetProperty("CMN_TenantCode") != null)
            {
                if (!string.IsNullOrEmpty((string)TObject.GetType().GetProperty("CMN_TenantCode").GetValue(TObject)))
                    IsValid = false;
            }
            return IsValid;
        }
        public virtual List<string> RestrictPropertyName<T>(T TObject, string fieldName)
        {
            List<string> prefixfieldName = new List<string>();
            List<string> lstFieldName = new List<string>();
            try
            {
                if (!string.IsNullOrEmpty(fieldName))
                    lstFieldName = fieldName.Split(new char[] { ',' }).ToList();

                foreach (var item in lstFieldName)
                {
                    string Prefix = string.Empty;
                    foreach (var prop in TObject.GetType().GetProperties())
                    {
                        if (prop.Name.Length > 4)
                            Prefix = prop.Name.Substring(0, 4);
                        else
                            Prefix = "";
                        if (Prefix.ToUpper() != "CMN_")
                            break;

                    }
                    prefixfieldName.Add(Prefix + item);
                }
                return prefixfieldName;
            }
            catch (Exception)
            {

                throw;
            }
        }
        public virtual T SetPropertyNull<T>(T TObject, List<string> PropertyName)
        {
            try
            {
                foreach (var item in PropertyName)
                {
                    if (TObject.GetType().GetProperty(item) != null)
                    {
                        System.Reflection.PropertyInfo p = typeof(T).GetProperty(item);
                        if (p != null)
                        {
                            System.Type t = p.PropertyType;

                            if (t == typeof(string))
                            {
                                p.SetValue(TObject, null);
                            }
                            else if (t == typeof(DateTime?))
                            {
                                p.SetValue(TObject, null);
                            }
                        }
                    }
                }
                return TObject;
            }
            catch (Exception)
            {

                throw;
            }

        }
        public virtual T AddDateTimeBasedOnProperty<T>(T TObject, string fieldName)
        {
            string PropertyName = string.Empty;
            List<string> lstfieldName = new List<string>();
            try
            {
                if (!string.IsNullOrEmpty(fieldName))
                    lstfieldName = fieldName.Split(new char[] { ',' }).ToList();

                foreach (var item in lstfieldName)
                {
                    string Prefix = string.Empty;
                    foreach (var prop in TObject.GetType().GetProperties())
                    {
                        if (prop.Name.Length > 4)
                            Prefix = prop.Name.Substring(0, 4);
                        else
                            Prefix = "";
                        if (Prefix.ToUpper() != "CMN_")
                            break;

                    }
                    PropertyName = Prefix + item;

                    //var dateTime = System.DateTime.UtcNow;
                    var dateTime = Convert.ToDateTime(System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.FFF"));

                    if (TObject.GetType().GetProperty(PropertyName) != null)
                    {
                        System.Reflection.PropertyInfo p = typeof(T).GetProperty(PropertyName);
                        if (p != null && dateTime != null)
                        {
                            System.Type t = p.PropertyType;

                            if (t == typeof(DateTime))
                            {
                                p.SetValue(TObject, dateTime);
                            }
                            else if (t == typeof(DateTime?))
                            {
                                p.SetValue(TObject, dateTime);
                            }
                        }
                    }
                }
                return TObject;
            }
            catch (Exception)
            {

                throw;
            }
        }
        public virtual T AddProperty<T>(T TObject, string fieldName)
        {
            string PropertyName = string.Empty;
            List<string> lstfieldName = new List<string>();
            try
            {
                if (!string.IsNullOrEmpty(fieldName))
                    lstfieldName = fieldName.Split(new char[] { ',' }).ToList();

                foreach (var item in lstfieldName)
                {
                    string Prefix = string.Empty;
                    foreach (var prop in TObject.GetType().GetProperties())
                    {
                        if (prop.Name.Length > 4)
                            Prefix = prop.Name.Substring(0, 4);
                        else
                            Prefix = "";
                        if (Prefix.ToUpper() != "CMN_")
                            break;

                    }
                    PropertyName = Prefix + item;

                    var UserName = string.Empty;
                    //if (TObject.GetType().GetProperty(PropertyName) != null)
                    //{
                    //    if (string.IsNullOrEmpty((string)TObject.GetType().GetProperty(PropertyName).GetValue(TObject)))
                    //    {
                    System.Reflection.PropertyInfo p = typeof(T).GetProperty(PropertyName);

                    UserName = GetUserName(UserName);

                    // check if property exists.
                    if (p != null && !string.IsNullOrEmpty(UserName))
                    {
                        System.Type t = p.PropertyType;

                        if (t == typeof(string))
                        {
                            p.SetValue(TObject, UserName);
                        }
                    }
                    //    }
                    //}
                }
                return TObject;
            }
            catch
            {
                throw;
            }
        }
        #endregion

        #region DataFilter's

        public virtual DataTable ConvertTo<T>(List<T> list)
        {
            DataTable table = CreateTable<T>();
            Type entityType = typeof(T);
            PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(entityType);

            foreach (T item in list)
            {
                DataRow row = table.NewRow();

                foreach (PropertyDescriptor prop in properties)
                {
                    row[prop.Name] = prop.GetValue(item);
                }

                table.Rows.Add(row);
            }

            return table;
        }
        public virtual List<T> ConvertTo<T>(List<DataRow> rows)
        {
            List<T> list = null;

            if (rows != null)
            {
                list = new List<T>();

                foreach (DataRow row in rows)
                {
                    T item = CreateItem<T>(row);
                    list.Add(item);
                }
            }

            return list;
        }
        public virtual List<T> ConvertTo<T>(DataTable table)
        {
            if (table == null)
            {
                return null;
            }

            List<DataRow> rows = new List<DataRow>();

            foreach (DataRow row in table.Rows)
            {
                rows.Add(row);
            }

            return ConvertTo<T>(rows);
        }
        public virtual T CreateItem<T>(DataRow row)
        {
            T obj = default(T);
            if (row != null)
            {
                obj = Activator.CreateInstance<T>();

                foreach (DataColumn column in row.Table.Columns)
                {
                    PropertyInfo prop = obj.GetType().GetProperty(column.ColumnName);
                    try
                    {
                        if (prop.PropertyType.IsGenericType && prop.PropertyType.Name.Contains("Nullable"))
                        {
                            if (!string.IsNullOrEmpty(row[prop.Name].ToString()))
                                prop.SetValue(obj, Convert.ChangeType(row[prop.Name],
                                Nullable.GetUnderlyingType(prop.PropertyType), null));
                            //else do nothing
                        }
                        else
                            prop.SetValue(obj, Convert.ChangeType(row[prop.Name], prop.PropertyType), null);

                        //object value = row[column.ColumnName];
                        //prop.SetValue
                        //    (obj, value.ToString(), null);
                    }
                    catch
                    {
                        // You can log something here
                        throw;
                    }
                }
            }

            return obj;
        }
        public virtual DataTable CreateTable<T>()
        {
            Type entityType = typeof(T);
            DataTable table = new DataTable(entityType.Name);
            PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(entityType);

            foreach (PropertyDescriptor prop in properties)
            {
                table.Columns.Add(prop.Name, prop.PropertyType);
            }

            return table;
        }

        #endregion

        #region Expression

        public virtual void TestExpression<T>(string propertyName, Expression<Func<T, bool>> predicate, string GroupBy) where T : class
        {
            try
            {
                string[] Fields = new string[] { "WKI_Status", "WKI_WSI_StepName" };
                string[] SelectFields = new string[] { "WKI_Status", "WKI_WSI_StepName" };
                var lambda = GroupByExpression<T>(Fields);
                var Selectlambda = SelectExpression<T>(SelectFields);
                var Test5 = _dbContext.Set<T>().AsQueryable<T>().GroupBy(lambda.Compile()).ToList();
                var source = new[]
                {
                    new SourceModelType (){ A = "hello", B = "world", C = "foo", D = "bar", E = "Baz" },
                    new SourceModelType (){ A = "The", B = "answer", C = "is", D = "42", E = "!" }
                };

                var dest = ProjectionMap<SourceModelType, DestModelType>(source.AsQueryable());
            }
            catch
            {
                Dispose();
                throw;
            }
        }
        public virtual IQueryable<TDest> ProjectionMap<TSource, TDest>(IQueryable<TSource> sourceModel) where TDest : new()
        {
            var sourceProperties = typeof(TSource).GetProperties().Where(p => p.CanRead);
            var destProperties = typeof(TDest).GetProperties().Where(p => p.CanWrite);
            var propertyMap = from d in destProperties
                              join s in sourceProperties on new { d.Name, d.PropertyType } equals new { s.Name, s.PropertyType }
                              select new { Source = s, Dest = d };
            var itemParam = Expression.Parameter(typeof(TSource), "item");
            var memberBindings = propertyMap.Select(p => (MemberBinding)Expression.Bind(p.Dest, Expression.Property(itemParam, p.Source)));
            var newExpression = Expression.New(typeof(TDest));
            var memberInitExpression = Expression.MemberInit(newExpression, memberBindings);
            var projection = Expression.Lambda<Func<TSource, TDest>>(memberInitExpression, itemParam);
            return sourceModel.Select(projection);
        }
        public virtual Expression<Func<T, object>> SelectExpression<T>(params string[] fieldNames)
        {
            var param = Expression.Parameter(typeof(T), "item");
            var fields = fieldNames.Select(x => Expression.Property(param, x)).ToArray();
            var types = fields.Select(x => x.Type).ToArray();
            var type = Type.GetType("System.Tuple`" + fields.Count() + ", mscorlib", true);
            var tuple = type.MakeGenericType(types);
            var ctor = tuple.GetConstructor(types);
            return Expression.Lambda<Func<T, object>>(
                Expression.New(ctor, fields),
                param
            );
        }
        public virtual Expression<Func<T, object>> GroupByExpression<T>(string[] propertyNames)
        {
            var properties = propertyNames.Select(name => typeof(T).GetProperty(name)).ToArray();
            var propertyTypes = properties.Select(p => p.PropertyType).ToArray();
            var tupleTypeDefinition = typeof(Tuple).Assembly.GetType("System.Tuple`" + properties.Length);
            var tupleType = tupleTypeDefinition.MakeGenericType(propertyTypes);
            var constructor = tupleType.GetConstructor(propertyTypes);
            var param = Expression.Parameter(typeof(T), "item");
            var body = Expression.New(constructor, properties.Select(p => Expression.Property(param, p)));
            var expr = Expression.Lambda<Func<T, object>>(body, param);
            return expr;
        }

        #endregion

        #region ORder by

        public static IOrderedQueryable<T> OrderBy<T>(IQueryable<T> source, string property)
        {
            return ApplyOrder<T>(source, property, "OrderBy");
        }
        public static IOrderedQueryable<T> OrderByDescending<T>(IQueryable<T> source, string property)
        {
            return ApplyOrder<T>(source, property, "OrderByDescending");
        }
        public static IOrderedQueryable<T> ThenBy<T>(IOrderedQueryable<T> source, string property)
        {
            return ApplyOrder<T>(source, property, "ThenBy");
        }
        public static IOrderedQueryable<T> ThenByDescending<T>(IOrderedQueryable<T> source, string property)
        {
            return ApplyOrder<T>(source, property, "ThenByDescending");
        }
        public static IOrderedQueryable<T> ApplyOrder<T>(IQueryable<T> source, string property, string methodName)
        {
            string[] props = property.Split('.');
            Type type = typeof(T);
            ParameterExpression arg = Expression.Parameter(type, "x");
            Expression expr = arg;
            foreach (string prop in props)
            {
                // use reflection (not ComponentModel) to mirror LINQ
                PropertyInfo pi = type.GetProperty(prop);
                expr = Expression.Property(expr, pi);
                type = pi.PropertyType;
            }
            Type delegateType = typeof(Func<,>).MakeGenericType(typeof(T), type);
            LambdaExpression lambda = Expression.Lambda(delegateType, expr, arg);

            object result = typeof(Queryable).GetMethods().Single(
                    method => method.Name == methodName
                            && method.IsGenericMethodDefinition
                            && method.GetGenericArguments().Length == 2
                            && method.GetParameters().Length == 2)
                    .MakeGenericMethod(typeof(T), type)
                    .Invoke(null, new object[] { source, lambda });
            return (IOrderedQueryable<T>)result;
        }

        #endregion

        #region Logs

        private void LogQueue(DbContext dbContext = null)
        {
            if (dbContext == null)
                dbContext = _dbContext;
            var QueueLog = TrustFactory.GetLogQueueInstance(dbContext);
            //bool IsQueueLog = Convert.ToBoolean(ConfigurationManager.AppSettings["IsQueueLog"]);
            //if (IsQueueLog)
            Task.Run(() => QueueLog.SaveQueueLogs());

        }
        private void LogQueue(DbContext dbContext, DbContextTransaction dbContextTransaction)
        {
            if (dbContext == null)
                dbContext = _dbContext;
            var QueueLog = TrustFactory.GetLogQueueInstance(dbContext);
            //bool IsQueueLog = Convert.ToBoolean(ConfigurationManager.AppSettings["IsQueueLog"]);
            //if (IsQueueLog)
            QueueLog.SaveQueueLogs(dbContext, dbContextTransaction);

        }
        public void CallAudit(DbContext dbContext = null)
        {
            bool IsAuditActive = Convert.ToBoolean(ConfigurationManager.AppSettings["IsAuditActive"]);
            if (IsAuditActive)
            {
                if (dbContext == null)
                    dbContext = _dbContext;

                var DataExtract = TrustFactory.GetDataExtractInstance(dbContext);
                DataExtract.SaveDataAudit();
                DataExtract.SaveDataEvent();
                DataExtract.SaveDataIntegration();
                DataExtract.SaveDataFullTextSearch();
                DataExtract.SaveDataSharedEntity(dbContext);
            }
        }

        private void PublishEvent()
        {
            var desEvents = TrustHelper.GetObjectFromRequestSession<List<DesEvent>>("DesEvents");
            if (desEvents != null)
            {
                var des = new DesEngine();
                des.desEvents = desEvents;
                des.PublishEvents();
                HttpContext.Current.Items.Remove("DesEvents");
            }
        }

        #endregion

        #region Database

        public string GetDataBaseName()
        {
            string result = string.Empty;

            result = _dbContext.Database.Connection.Database;

            return result;
        }
        public string GetTableName<T>() where T : class
        {
            string tableName = string.Empty;
            var entitySet = GetEntitySet<T>(_dbContext);
            if (entitySet != null)
                tableName = GetStringProperty(entitySet, "Table");
            return tableName;
        }
        private string GetStringProperty(MetadataItem entitySet, string propertyName)
        {
            MetadataProperty property;
            if (entitySet == null)
                throw new ArgumentNullException("entitySet");
            if (entitySet.MetadataProperties.TryGetValue(propertyName, false, out property))
            {
                string str = null;
                if (((property != null) &&
                    (property.Value != null)) &&
                    (((str = property.Value as string) != null) &&
                    !string.IsNullOrEmpty(str)))
                {
                    return str;
                }
            }
            return string.Empty;
        }
        public EntitySet GetEntitySet<T>(DbContext context)
        {
            var type = typeof(T);
            var entityName = type.Name;
            var metadata = ((IObjectContextAdapter)context).ObjectContext.MetadataWorkspace;

            IEnumerable<EntitySet> entitySets;
            entitySets = metadata.GetItemCollection(DataSpace.SSpace)
                             .GetItems<EntityContainer>()
                             .Single()
                             .BaseEntitySets
                             .OfType<EntitySet>()
                             .Where(s => !s.MetadataProperties.Contains("Type")
                                         || s.MetadataProperties["Type"].ToString() == "Tables");
            var entitySet = entitySets.FirstOrDefault(t => t.Name == entityName);
            return entitySet;
        }

        #endregion

        #region Not Used

        public Func<T, object> GenericEvaluateOrderBy<T>(string propertyName)
        {
            var type = typeof(T);
            var parameter = Expression.Parameter(type, "p");
            var propertyReference = Expression.Property(parameter,
                    propertyName);
            return Expression.Lambda<Func<T, object>>
                    (propertyReference, new[] { parameter }).Compile();
        }
        private static Expression GetConvertedSource(ParameterExpression sourceParameter, PropertyInfo sourceProperty, TypeCode typeCode)
        {
            var sourceExpressionProperty = Expression.Property(sourceParameter,
                                                               sourceProperty);

            var changeTypeCall = Expression.Call(typeof(Convert).GetMethod("ChangeType",
                                                                   new[] { typeof(object),
                                                            typeof(TypeCode) }),
                                                                    sourceExpressionProperty,
                                                                    Expression.Constant(typeCode)
                                                                    );
            Expression convert = Expression.Convert(changeTypeCall,
                                                    Type.GetType("System." + typeCode));

            var convertExpr = Expression.Condition(Expression.Equal(sourceExpressionProperty,
                                                    Expression.Constant(null, sourceProperty.PropertyType)),
                                                    Expression.Default(Type.GetType("System." + typeCode)),
                                                    convert);
            return convertExpr;
        }
        public Expression GetLambda<T>(string property)
        {
            var param = Expression.Parameter(typeof(T), "p");

            Expression parent = Expression.Property(param, property);

            if (!parent.Type.IsValueType)
            {
                return Expression.Lambda<Func<T, object>>(parent, param);
            }
            var convert = Expression.Convert(parent, typeof(object));
            return Expression.Lambda<Func<T, object>>(convert, param);
        }
        public virtual Expression<Func<T, bool>> GetExperssion<T>(string fieldName, string FieldVal = "")
        {
            Expression exp = null;
            ParameterExpression param = Expression.Parameter(typeof(T), "t");
            MemberExpression member = Expression.Property(param, fieldName);
            ConstantExpression constant = null;

            if (!string.IsNullOrWhiteSpace(FieldVal))
            {
                constant = Expression.Constant(new Guid(FieldVal));
                exp = Expression.Equal(member, constant);
            }
            return Expression.Lambda<Func<T, bool>>(exp, param);
        }
        #endregion

        #region Get Query
        public virtual string GetQuery<T>(Expression<Func<T, bool>> predicate) where T : class
        {
            if (predicate == null)
                return _dbContext.Set<T>().ToString();
            else
                return _dbContext.Set<T>().Where<T>(predicate).ToString();
        }
        #endregion

        #region Data Table
        /// <summary>
        /// This method will convert as Data Table from list of class (T) objects
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <returns>DataTable</returns>
        /// <author>Gobinath Rajendran</author>
        public DataTable ToDataTable<T>(List<T> lstEntities) where T : class
        {
            string entityTableName = string.Empty;
            if (lstEntities != null && lstEntities.Count > 0)
                entityTableName = LogQueueHelper.GetTableNameByEntity<T>(lstEntities[0]);
            DataTable dataTable = new DataTable(typeof(T).Name);
            PropertyInfo[] Props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo prop in Props)
            {
                // Check if the property has the NotMappedAttribute
                if (!Attribute.IsDefined(prop, typeof(NotMappedAttribute)) && !prop.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Any())
                {
                    // Add the property as a column in the DataTable if not marked with [NotMapped]
                    dataTable.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                }
            }
            //foreach (PropertyInfo prop in Props)
            //    dataTable.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
            foreach (T objSingleEntity in lstEntities)
            {
                var values = new List<object>();

                foreach (PropertyInfo prop in Props)
                {
                    if (!Attribute.IsDefined(prop, typeof(NotMappedAttribute)) && !prop.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Any())
                    {
                        // Add property values to values list if not marked with [NotMapped]
                        values.Add(prop.GetValue(objSingleEntity, null));
                    }
                }

                // Add row to the DataTable only with values that are not marked as [NotMapped]
                dataTable.Rows.Add(values.ToArray());
            }
            //Bind Entity's table name to data table object
            if (dataTable != null && (!string.IsNullOrEmpty(entityTableName)))
                dataTable.TableName = entityTableName;
            return dataTable;
        }
        /// <summary>
        /// This method will insert the Data Table object to SQL directly using DB context
        /// </summary>
        /// <param name="objDataTable"></param>
        /// <author>Gobinath Rajendran</author>
        public void SqlBulkCopy(DataTable objDataTable)
        {
            using (var bulkInsert = new SqlBulkCopy(ConfigurationManager.ConnectionStrings["eAxisDBContext"].ToString(), SqlBulkCopyOptions.UseInternalTransaction))
            {
                bulkInsert.BulkCopyTimeout = 300;
                bulkInsert.DestinationTableName = objDataTable.TableName;
                bulkInsert.WriteToServer(objDataTable);
            }
        }
        public void SqlBulkCopy(DataTable objDataTable, string appCode = null)
        {
            SqlConnection connection = null;

            try
            {
                // Open the connection using DbContext
                connection = (SqlConnection)_dbContext.Database.Connection;
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }

                using (var sqlBulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, null))
                {
                    sqlBulkCopy.BulkCopyTimeout = 300;
                    sqlBulkCopy.BatchSize = 10000; // Adjust based on your scenario
                    sqlBulkCopy.DestinationTableName = objDataTable.TableName;

                    // Map columns from DataTable to the destination table
                    foreach (DataColumn column in objDataTable.Columns)
                    {
                        sqlBulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                    }

                    // Execute the bulk copy operation
                    sqlBulkCopy.WriteToServer(objDataTable);
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                // Ensure the connection is closed and DbContext is disposed of
                if (connection != null && connection.State == ConnectionState.Open)
                {
                    connection.Close();
                }
                if (_dbContext != null)
                {
                    _dbContext.Dispose();
                }
            }
        }


        public void SqlBulkCopyWithTransaction(DataTable objDataTable, SqlConnection destinationConnection, SqlTransaction transaction)
        {
            using (SqlBulkCopy bulkInsert = new SqlBulkCopy(destinationConnection, SqlBulkCopyOptions.KeepIdentity, transaction))
            {
                bulkInsert.BulkCopyTimeout = 300;
                bulkInsert.DestinationTableName = objDataTable.TableName;
                bulkInsert.WriteToServer(objDataTable);
            }
        }
        public void SqlBulkCopyWithTransaction(DataTable objDataTable, SqlConnection destinationConnection)
        {
            using (var bulkInsert = new SqlBulkCopy(destinationConnection))
            {
                bulkInsert.BulkCopyTimeout = 300;
                bulkInsert.BatchSize = 1000;
                bulkInsert.DestinationTableName = objDataTable.TableName;
                bulkInsert.WriteToServer(objDataTable);
            }
        }
        public void SqlBulkOperationWithTrans(DataTable objDataTable, SqlConnection destinationConnection, string OperationType)
        {
            StringBuilder _stringBuilderQuery = new StringBuilder();
            var ds = new DataSet();
            List<string> columnNameWithoutPK = new List<string>();
            string co = string.Empty;
            List<string> col = new List<string>();
            var _tableName = objDataTable.TableName;

            //Find Primary Key
            _stringBuilderQuery.Append("SELECT C.COLUMN_NAME FROM  INFORMATION_SCHEMA.TABLE_CONSTRAINTS T  JOIN INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE C  ON C.CONSTRAINT_NAME=T.CONSTRAINT_NAME  ");
            _stringBuilderQuery.Append(" WHERE  C.TABLE_NAME='" + objDataTable.TableName + "' and T.CONSTRAINT_TYPE='PRIMARY KEY'");
            using (SqlCommand cmd = new SqlCommand(_stringBuilderQuery.ToString(), destinationConnection))
            using (var adapter = new SqlDataAdapter(cmd))
            {
                cmd.CommandTimeout = 600;
                adapter.Fill(ds);
            }
            DataTable res = ds.Tables[0];
            string _primaryKeyColumn = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(JsonConvert.SerializeObject(res))[0]["COLUMN_NAME"];

            // Take All Columns
            List<string> columnNames = objDataTable.Columns.Cast<DataColumn>()
                                 .Select(x => x.ColumnName)
                                 .ToList();
            using (TransactionScope transactionScopes = new TransactionScope(TransactionScopeOption.Suppress))
            {
                _stringBuilderQuery = new StringBuilder();
                if (OperationType == "Update")
                    _stringBuilderQuery.Append("SELECT * INTO " + objDataTable.TableName + "_TEMP FROM " + objDataTable.TableName + " WHERE " + _primaryKeyColumn + " IS NULL");
                else
                {
                    _stringBuilderQuery.Append("SELECT " + _primaryKeyColumn + " INTO " + objDataTable.TableName + "_TEMP FROM " + objDataTable.TableName + " WHERE " + _primaryKeyColumn + " IS NULL");
                    DataView dv = new DataView(objDataTable);
                    objDataTable = dv.ToTable(true, _primaryKeyColumn);
                }

                using (SqlCommand cmd = new SqlCommand(_stringBuilderQuery.ToString(), destinationConnection))
                {
                    cmd.CommandText = _stringBuilderQuery.ToString();
                    cmd.CommandType = CommandType.Text;
                    cmd.ExecuteNonQuery();
                }
                objDataTable.TableName = _tableName + "_TEMP";
                using (var bulkInsert = new SqlBulkCopy(destinationConnection))
                {
                    bulkInsert.BulkCopyTimeout = 300;
                    bulkInsert.BatchSize = 1000;
                    bulkInsert.DestinationTableName = objDataTable.TableName;
                    bulkInsert.WriteToServer(objDataTable);
                }
                transactionScopes.Complete();
            }
            if (OperationType == "Update")
            {
                columnNameWithoutPK.AddRange(columnNames);

                columnNameWithoutPK.Remove(_primaryKeyColumn);

                for (int i = 0; i < columnNameWithoutPK.Count(); i++)
                {
                    co = "[" + columnNameWithoutPK[i] + "] = StagingTable.[" + columnNameWithoutPK[i] + "]";
                    if (!columnNameWithoutPK[i].Contains("RowVersion")) { 
                        col.Add(co);
                    }
                }
                string columns = string.Join(",", col.Select(x => x));
                _stringBuilderQuery = new StringBuilder();
                //Final Update Query
                _stringBuilderQuery.Append(" exec sp_executesql N'UPDATE DestinationTable SET " + columns + " FROM " + _tableName + " AS DestinationTable INNER JOIN " + objDataTable.TableName +
              " AS StagingTable ON DestinationTable." + _primaryKeyColumn + " = StagingTable." + _primaryKeyColumn + ";'");

            }
            if (OperationType == "Delete")
            {
                _stringBuilderQuery = new StringBuilder();
                //Final Delete Query
                _stringBuilderQuery.Append(" exec sp_executesql N'DELETE DestinationTable FROM " + _tableName + " AS DestinationTable INNER JOIN " + objDataTable.TableName +
            " AS StagingTable ON DestinationTable." + _primaryKeyColumn + " = StagingTable." + _primaryKeyColumn + ";'");

            }
            // Executing Final Query of  Update / Delete
            using (SqlCommand cmd = new SqlCommand(_stringBuilderQuery.ToString(), destinationConnection))
            {
                cmd.CommandText = _stringBuilderQuery.ToString();
                cmd.CommandType = CommandType.Text;
                cmd.ExecuteNonQuery();
            }

            using (TransactionScope transactionScopes = new TransactionScope(TransactionScopeOption.Suppress))
            {
                _stringBuilderQuery = new StringBuilder();
                _stringBuilderQuery.Append(" DROP TABLE " + objDataTable.TableName);
                using (SqlCommand cmd = new SqlCommand(_stringBuilderQuery.ToString(), destinationConnection))
                {
                    cmd.CommandText = _stringBuilderQuery.ToString();
                    cmd.CommandType = CommandType.Text;
                    cmd.ExecuteNonQuery();
                }
                transactionScopes.Complete();
            }
        }

        #endregion

        #region Transaction Scope for 3PL Test
        public void SqlBulkCopyWithTransactionInsertWithDES<T>(List<T> TListObject, DataTable objDataTable, SqlConnection destinationConnection, bool IsDES) where T : class
        {
            using (var bulkInsert = new SqlBulkCopy(destinationConnection))
            {
                if (TListObject?.Count > 0 && IsDES)
                {
                    DESAction(TListObject, destinationConnection, "Insert");
                }

                bulkInsert.BulkCopyTimeout = 300;
                bulkInsert.BatchSize = 1000;
                bulkInsert.DestinationTableName = objDataTable.TableName;
                bulkInsert.WriteToServer(objDataTable);
            }
        }
        public void SqlBulkOperationWithTransWithDES<T>(List<T> TListObject, DataTable objDataTable, SqlConnection destinationConnection, string OperationType, bool IsDES) where T : class
        {
            try
            {
                if (TListObject?.Count > 0 && IsDES)
                {
                    DESAction(TListObject, destinationConnection, OperationType);
                }
                SqlBulkOperationWithTrans(objDataTable, destinationConnection, OperationType);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public void DESAction<T>(List<T> TListObject, SqlConnection destinationConnection, string OperationType) where T : class
        {
            string tableName = LogQueueHelper.GetTableName(TListObject.First());
            if (!string.IsNullOrEmpty(tableName) && OperationType != "Delete")
            {
                Int32 sharedEntityCount = 0;
                SharedEntity objSharedEntity = new SharedEntity();
                List<DataSharedEntity> listSharedEntity = new List<DataSharedEntity>();

                TListObject.ForEach(TObject =>
                {
                    //*******DESV2************
                    #region DES PreProcessing
                    //Starting the DES preprocessing which will check if there is any fields configured for events asynchronously and returns the changed fields
                    var desEntity = new DesEntity();
                    desEntity.Context = _dbContext;
                    desEntity.TenantCode = GetTenantCode();
                    desEntity.Entity = TObject;
                    if (OperationType == "Insert")
                        desEntity.Action = "I";
                    if (OperationType == "Update")
                        desEntity.Action = "U";
                    using (TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Suppress, new TransactionOptions
                    {
                        IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted
                    }))
                    {
                        //Shared Entity Object Preparation
                        sharedEntityCount = CheckSharedEntityCount(TObject);
                        if (sharedEntityCount > 0)
                        {
                            if (OperationType == "Insert")
                                listSharedEntity.AddRange(objSharedEntity.PrepareJobSharedEntityDetails(TObject, "I"));
                            if (OperationType == "Update")
                                listSharedEntity.AddRange(objSharedEntity.PrepareJobSharedEntityDetails(TObject, "U"));
                        }
                        var des = new RuleEngine();
                        des.desEntity = desEntity;
                        des.PreProcessing(TObject);
                        //Task<Dictionary<string, object>> desPreProcessingTask = Task.Factory.StartNew(()=> des.PreProcessing(TObject));
                        #endregion

                        #region Invoke Des Process
                        //*******DESV2************
                        //Wait for the DES Preprocessing to complete and get the changed fields. If there are any fields then call the DES Process
                        //desPreProcessingTask.Wait();
                        var desEvents = des.Execute();
                        if (desEvents != null && desEvents.Count > 0)
                            TrustHelper.SetObjectInRequestSession<DesEvent>("DesEvents_Transaction", desEvents);
                        #endregion
                        transactionScope.Complete();
                    }
                });

                if (listSharedEntity?.Count > 0)
                {
                    DataTable _DataTable = ToDataTable(listSharedEntity);
                    if (OperationType == "Insert")
                        SqlBulkCopyWithTransactionInsertWithDES(listSharedEntity, _DataTable, destinationConnection, false);
                    if (OperationType == "Update")
                        SqlBulkOperationWithTransWithDES(listSharedEntity, _DataTable, destinationConnection, "Update", false);
                }
            }
        }
        public void SqlBulkOperationWithTransV1(DataTable objDataTable, SqlConnection destinationConnection, string OperationType)
        {
            StringBuilder _stringBuilderQuery = new StringBuilder();
            var ds = new DataSet();
            List<string> columnNameWithoutPK = new List<string>();
            string co = string.Empty;
            List<string> col = new List<string>();
            var _tableName = objDataTable.TableName;

            //Find Primary Key
            _stringBuilderQuery.Append("SELECT C.COLUMN_NAME FROM  INFORMATION_SCHEMA.TABLE_CONSTRAINTS T  JOIN INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE C  ON C.CONSTRAINT_NAME=T.CONSTRAINT_NAME  ");
            _stringBuilderQuery.Append(" WHERE  C.TABLE_NAME='" + objDataTable.TableName + "' and T.CONSTRAINT_TYPE='PRIMARY KEY'");
            using (SqlCommand cmd = new SqlCommand(_stringBuilderQuery.ToString(), destinationConnection))
            using (var adapter = new SqlDataAdapter(cmd))
            {
                cmd.CommandTimeout = 600;
                adapter.Fill(ds);
            }
            DataTable res = ds.Tables[0];
            string _primaryKeyColumn = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(JsonConvert.SerializeObject(res))[0]["COLUMN_NAME"];

            // Take All Columns
            List<string> columnNames = objDataTable.Columns.Cast<DataColumn>()
                                 .Select(x => x.ColumnName)
                                 .ToList();
            using (TransactionScope transactionScopes = new TransactionScope(TransactionScopeOption.Suppress))
            {
                _stringBuilderQuery = new StringBuilder();
                _stringBuilderQuery.Append("SELECT * INTO " + objDataTable.TableName + "_TEMP FROM " + objDataTable.TableName + " WHERE " + _primaryKeyColumn + " IS NULL");
                using (SqlCommand cmd = new SqlCommand(_stringBuilderQuery.ToString(), destinationConnection))
                {
                    cmd.CommandText = _stringBuilderQuery.ToString();
                    cmd.CommandType = CommandType.Text;
                    cmd.ExecuteNonQuery();
                }
                objDataTable.TableName = _tableName + "_TEMP";
                using (var bulkInsert = new SqlBulkCopy(destinationConnection))
                {
                    bulkInsert.BulkCopyTimeout = 300;
                    bulkInsert.BatchSize = 1000;
                    bulkInsert.DestinationTableName = objDataTable.TableName;
                    bulkInsert.WriteToServer(objDataTable);
                }
                transactionScopes.Complete();
            }
            if (OperationType == "Update")
            {
                columnNameWithoutPK.AddRange(columnNames);

                columnNameWithoutPK.Remove(_primaryKeyColumn);

                for (int i = 0; i < columnNameWithoutPK.Count(); i++)
                {
                    co = "[" + columnNameWithoutPK[i] + "] = StagingTable.[" + columnNameWithoutPK[i] + "]";
                    if (!columnNameWithoutPK[i].Contains("RowVersion"))
                        col.Add(co);
                }
                string columns = string.Join(",", col.Select(x => x));
                _stringBuilderQuery = new StringBuilder();
                //Final Update Query
                _stringBuilderQuery.Append(" exec sp_executesql N'UPDATE DestinationTable SET " + columns + " FROM " + _tableName + " AS DestinationTable INNER JOIN ( SELECT * FROM " + objDataTable.TableName +
              ") AS StagingTable ON DestinationTable." + _primaryKeyColumn + " = StagingTable." + _primaryKeyColumn + ";'");

            }
            if (OperationType == "Delete")
            {
                _stringBuilderQuery = new StringBuilder();
                //Final Delete Query
                _stringBuilderQuery.Append(" exec sp_executesql N'DELETE DestinationTable FROM " + _tableName + " AS DestinationTable INNER JOIN ( SELECT " + _primaryKeyColumn + " FROM " + objDataTable.TableName +
            ") AS StagingTable ON DestinationTable." + _primaryKeyColumn + " = StagingTable." + _primaryKeyColumn + ";'");

            }
            // Executing Final Query of  Update / Delete
            using (SqlCommand cmd = new SqlCommand(_stringBuilderQuery.ToString(), destinationConnection))
            {
                cmd.CommandText = _stringBuilderQuery.ToString();
                cmd.CommandType = CommandType.Text;
                cmd.ExecuteNonQuery();
            }

            using (TransactionScope transactionScopes = new TransactionScope(TransactionScopeOption.Suppress))
            {
                _stringBuilderQuery = new StringBuilder();
                _stringBuilderQuery.Append(" DROP TABLE " + objDataTable.TableName);
                using (SqlCommand cmd = new SqlCommand(_stringBuilderQuery.ToString(), destinationConnection))
                {
                    cmd.CommandText = _stringBuilderQuery.ToString();
                    cmd.CommandType = CommandType.Text;
                    cmd.ExecuteNonQuery();
                }
                transactionScopes.Complete();
            }
        }
        #endregion

        #region REDIS CACHE

        public virtual IQueryable<T> GetDataFromCache<T>(Expression<Func<T, bool>> predicate, FilterInput lstFilterInput = null) where T : class
        {
            IQueryable<T> result = Enumerable.Empty<T>().AsQueryable();
            CacheService = CacheFactory.GetMemoryCacheInstance("redis");
            ConfigHelper configHelper = new ConfigHelper(ConfigFileIndex.CommonConfig);

            object isCacheEnabled; object ExpirationInSeconds;

            isCacheEnabled = configHelper.TryGetValue("RedisCacheConfigurations.isRedisCacheEnabled", out _isRedisCacheEnabled);
            _environment = Convert.ToString(ConfigurationManager.AppSettings["Environment"]);

            //Key Formation
            var key = _environment + '_' + lstFilterInput.CacheKey;

            var cacheData = CacheService.GetData(key);
            if (_isRedisCacheEnabled?.ToString() == "true" && !string.IsNullOrEmpty(cacheData?.ToString()) && cacheData != "{}" && cacheData != "[{}]")
            {
                List<T> CacheResponse = JsonConvert.DeserializeObject<List<T>>(cacheData?.ToString());

                if (CacheResponse?.Count > 0)
                    result = CacheResponse?.AsQueryable();
            }
            else
            {
                bool returnvalue = configHelper.TryGetValue("RedisCacheConfigurations.ExpirationInSeconds", out ExpirationInSeconds);
                int expireInSeconds = ExpirationInSeconds.GetInt32OrDefault(0);

                result = _dbContext.Set<T>().Where<T>(predicate).AsQueryable<T>().AsNoTracking();

                if (_isRedisCacheEnabled?.ToString() == "true" && result != null)
                    CacheService.AddDataWithAbsoluteExpiration(key, result?.ToList(), expireInSeconds);
            }
            return result;
        }

        #endregion


    }
    public static class UpdateContext
    {
        #region Attach Entity or Check already available
        public static void AttachToOrGet<T>(this ObjectContext context, string entitySetName, ref T entity) where T : class
        {
            ObjectStateEntry entry;
            // Track whether we need to perform an attach            
            if (
                context.ObjectStateManager.TryGetObjectStateEntry
                    (
                        context.CreateEntityKey(entitySetName, entity),
                        out entry
                    )
                )
            {
                entry.ChangeState(EntityState.Detached);
            }


        }
        #endregion
    }

}
