using System;
using System.ComponentModel;
using System.Data.Common;

namespace MapsDirectlyToDatabaseTable
{
    /// <summary>
    /// Indicates that a class cannot exist in memory without simultaneously existing as a record in a database table.  This is how RDMP handles continuous access
    /// by multiple users and persistence of objects as well as allowing for enforcing program logic via database constraints.  
    /// 
    /// <para>RDMP basically treats the database as main memory and has many classes which are directly checked out, modified and saved into the database.  These 
    /// classes must follow strict rules e.g. all public properties must directly match columns in the database table holding them (See DatabaseEntity).  This is
    /// done in order to prevent corruption / race conditions / data loass etc in a multi user environment.</para>
    /// </summary>
    public interface IMapsDirectlyToDatabaseTable : IDeleteable
    {
        /// <summary>
        /// Every database table that stores an <see cref="IMapsDirectlyToDatabaseTable"/> must have an identity column called ID which must be the primary key.
        /// Therefore for a given <see cref="IRepository"/> this uniquely identifies a given object.
        /// </summary>
        int ID { get; set; }
        
        /// <summary>
        /// The persistence database that stores the object.  For example a <see cref="TableRepository"/>.
        /// </summary>
        [NoMappingToDatabase]
        IRepository Repository { get; set; }

        /// <summary>
        /// Event called when any persistent Property is changed to a new unique value (different than it's previous value)
        /// </summary>
        event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Makes any persistent Proporty change attempts throw an Exception.  (See also <see cref="PropertyChanged"/>)
        /// </summary>
        void SetReadOnly();

        //you must have a Property for each thing in your database table (With the same name)

        //you may have a public static field called X_MaxLength for each of these Properties

        //use MapsDirectlyToDatabaseTableRepository to fully utilise this interface

        //ensure you have a the same class name as the table name DIRECTLY
        //ensure you have a constructor that initializes your object when passed a DbDataReader (paramter value) and DbCommand (how to update yourself)
        //these two things are required for MapsDirectlyToDatabaseTable.GetAllObjects and MapsDirectlyToDatabaseTable.GetObjectByID
    }
}
