using Microsoft.EntityFrameworkCore;
using MediaSet.Books;

namespace MediaSet;

public class MediaSetDbContext : DbContext
{
    public DbSet<Book> Books { get; set; }

    private string DatabasePath { get; }
    
    public MediaSetDbContext()
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        this.DatabasePath = System.IO.Path.Join(path, "mediaset.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlite($"Data Source=>{this.DatabasePath}");
}