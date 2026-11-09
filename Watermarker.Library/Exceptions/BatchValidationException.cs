using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Watermarker.Library.Exceptions
{
    public enum BatchValidationError { WatermarkNotFound, InvalidWatermarkFormat, SourceFolderNotFound, DestinationSameAsSource, NewOriginalsSameAsSource, NewOriginalsSameAsDestination, InvalidOpacity }

    public class BatchValidationException(BatchValidationError validationError) : ApplicationException()
    {
        public static void ThrowIf(bool expression, BatchValidationError throwIfTrue)
        {
            if(expression)
            {
                throw new BatchValidationException(throwIfTrue);
            }
        }

        public readonly BatchValidationError ValidationError = validationError;
    }
}
