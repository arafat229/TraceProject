namespace TraceProject.Model
{
    public class Department 
    {
        public long Id { get; set; }
        public List<Employee> Employees { get; set; } = null!;
        public string Name { get; set; } = null!;
    }
}
