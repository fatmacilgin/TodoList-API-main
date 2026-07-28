namespace TodoList.Entities.DTOs;

public record UserRegisterDto(
    string FirstName,
    string LastName,
    DateTime BirthDate,
    string Email,
    string Password
);

public record UserLoginDto(
    string Email,
    string Password
);

public record AuthResponseDto(
    string Token,
    string FirstName,
    string LastName
);