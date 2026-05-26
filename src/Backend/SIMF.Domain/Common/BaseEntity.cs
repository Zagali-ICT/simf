using System;
using System.Collections.Generic;
using System.Text;

namespace SIMF.Domain.Common
{
    public abstract class BaseEntity
    {
        public Guid Id { get; set; }


        //
        public DateTimeOffset CreatedAt { get; set; }
        public Guid CreateBy { get; set; }


        //
        public DateTimeOffset? UpdateAt { get; set; }
        public Guid? UpdateBy { get; set; }

    }
}
