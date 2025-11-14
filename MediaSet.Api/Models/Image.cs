using System;

namespace MediaSet.Api.Models
{
    /// <summary>
    /// Represents image metadata for a media entity (cover image).
    /// </summary>
    public class Image
    {
        /// <summary>
        /// The original filename of the uploaded image.
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// The relative file path where the image is stored.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// The MIME type of the image (e.g., image/jpeg, image/png).
        /// </summary>
        public string ContentType { get; set; } = string.Empty;

        /// <summary>
        /// The size of the image file in bytes.
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// The timestamp when the image was created/uploaded.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// The timestamp when the image was last updated.
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }
}
