type AboutProps = {
  values: { title: string };
};

export default function About(props: AboutProps) {
  return (
    <main>
      <p class="eyebrow">DamascusUI / TSX routes</p>
      <h1>About {props.values.title}</h1>
      <p class="summary">This page was discovered from src/routes/about.tsx.</p>
    </main>
  );
}
