using Microsoft.EntityFrameworkCore;
using repetitorbot.Entities;
using repetitorbot.Entities.States;

namespace repetitorbot
{
    internal class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Quiz> Quizes { get; set; } = null!;
        public DbSet<QuizQuestion> QuizQuestions { get; set; } = null!; 
        public DbSet<State> States { get; set; } = null!;
        public DbSet<UserQuizQuestion> UserQuizQuestions { get; set; } = null!;
        public DbSet<QuizQuestionCategory> QuizQuestionCategories { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<State>()
                .HasDiscriminator<string>("Type")
                .HasValue<QuizState>("QuizState")
                .HasValue<CreateQuizState>("CreateQuizState")
                .HasValue<PublishSelectQuizState>("PublishSelectQuizState")
                .HasValue<StartSelectQuizState>("StartSelectQuizState")
                .HasValue<QuestionsSelectQuizState>("QuestionsSelectQuizState");
            modelBuilder.Entity<QuizQuestion>()
                .HasDiscriminator<string>("Type")
                .HasValue<TextQuizQuestion>("TextQuizQuestion")
                .HasValue<PollQuizQuestion>("PollQuizQuestion");
            modelBuilder.Entity<User>()
                .HasOne(x => x.State)
                .WithOne(x => x.User)
                .HasForeignKey<State>(x => x.UserId);

            modelBuilder.Entity<QuizQuestionCategoryLink>()
                .HasKey(x => new { x.QuizQuestionId, x.QuizQuestionCategoryId });

            base.OnModelCreating(modelBuilder);
        }
    }
}
